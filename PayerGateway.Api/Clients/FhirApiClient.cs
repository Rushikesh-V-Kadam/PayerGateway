using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Fhir;

namespace PayerGateway.Api.Clients;

/// <summary>
/// Talks to the Provider FHIR API (the gateway never calls the EHR's read APIs itself). Every call sends X-Internal-Key.
///   GET  {fhir}/{type}/{id}, {fhir}/{type}?...     Patient, Coverage, orders, Organization, documents, forms
///   PUT  {fhir}/QuestionnaireResponse/{id}          saves a DTR form built by the gateway
///   POST {root}/internal/access-tokens              fhirAuthorization token for a payer (CRD)
///   POST {root}/internal/smart-launches             SMART launch for a payer DTR app (future)
/// </summary>
public class FhirApiClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly FhirApiSettings _settings;

    /// <summary>Created by dependency injection.</summary>
    public FhirApiClient(IHttpClientFactory httpFactory, IOptions<FhirApiSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    /// <summary>FHIR base URL of the Provider FHIR API (also sent to payers as fhirServer).</summary>
    public string GetBaseUrl()
    {
        return _settings.GetBaseUrl();
    }

    // ================================================================== reads

    /// <summary>GET {fhir}/{type}/{id}. Null when the resource does not exist.</summary>
    public async Task<JsonObject?> ReadAsync(string resourceType, string id, CancellationToken ct)
    {
        string url = GetBaseUrl() + "/" + resourceType + "/" + Uri.EscapeDataString(id);
        JsonNode? node = await GetJsonAsync(url, ct);
        return node as JsonObject;
    }

    /// <summary>GET {fhir}/{query}, e.g. "Coverage?patient=pat-1&amp;status=active". Returns the searchset Bundle.</summary>
    public async Task<Bundle> SearchAsync(string query, CancellationToken ct)
    {
        string url = GetBaseUrl() + "/" + query.TrimStart('/');
        JsonNode? node = await GetJsonAsync(url, ct);
        Bundle? bundle = FhirJson.Read<Bundle>(node);
        if (bundle == null)
        {
            return new Bundle();
        }
        return bundle;
    }

    /// <summary>Runs a filled-in CRD prefetch query and returns whatever came back (a resource or a Bundle).</summary>
    public async Task<JsonNode?> PrefetchAsync(string query, CancellationToken ct)
    {
        string url = GetBaseUrl() + "/" + query.TrimStart('/');
        return await GetJsonAsync(url, ct);
    }

    /// <summary>The patient's active coverages.</summary>
    public async Task<List<Coverage>> GetActiveCoveragesAsync(string patientId, CancellationToken ct)
    {
        Bundle bundle = await SearchAsync("Coverage?patient=" + Uri.EscapeDataString(patientId) + "&status=active", ct);
        List<Coverage> coverages = new List<Coverage>();
        foreach (JsonObject raw in bundle.ResourcesOfType("Coverage"))
        {
            Coverage? coverage = FhirJson.Read<Coverage>(raw);
            if (coverage != null)
            {
                coverages.Add(coverage);
            }
        }
        return coverages;
    }

    /// <summary>
    /// Reads orders by EHR order id. An order can be a ServiceRequest, DeviceRequest or MedicationRequest,
    /// so each type is searched with _id=id1,id2. Orders that do not exist are simply missing from the result.
    /// </summary>
    public async Task<List<JsonObject>> ReadOrdersAsync(List<string> orderIds, CancellationToken ct)
    {
        List<JsonObject> orders = new List<JsonObject>();
        List<string> escaped = new List<string>();
        foreach (string orderId in TextHelper.Distinct(orderIds))
        {
            escaped.Add(Uri.EscapeDataString(orderId));
        }
        if (escaped.Count == 0)
        {
            return orders;
        }

        string ids = string.Join(",", escaped);
        string[] orderTypes = new string[] { "ServiceRequest", "DeviceRequest", "MedicationRequest" };
        foreach (string orderType in orderTypes)
        {
            Bundle bundle = await SearchAsync(orderType + "?_id=" + ids, ct);
            orders.AddRange(bundle.ResourcesOfType(orderType));
        }
        return orders;
    }

    /// <summary>
    /// Reads a DocumentReference and embeds the file (attachment.data) by downloading its Binary,
    /// because payers must receive the content itself (PAS / CDex), not a link to our server.
    /// </summary>
    public async Task<JsonObject?> ReadDocumentWithContentAsync(string documentId, CancellationToken ct)
    {
        JsonObject? document = await ReadAsync("DocumentReference", documentId, ct);
        if (document == null)
        {
            return null;
        }
        JsonArray? contents = document["content"] as JsonArray;
        if (contents == null)
        {
            return document;
        }

        foreach (JsonNode? content in contents)
        {
            JsonObject? attachment = null;
            if (content != null)
            {
                attachment = content["attachment"] as JsonObject;
            }
            if (attachment == null || attachment["data"] != null)
            {
                continue;
            }
            string? url = FhirJson.GetString(attachment, "url");
            if (url == null)
            {
                continue;
            }

            JsonNode? binary = await GetJsonAsync(url, ct);
            attachment["data"] = FhirJson.GetString(binary, "data");
            if (attachment["contentType"] == null)
            {
                attachment["contentType"] = FhirJson.GetString(binary, "contentType");
            }
            attachment.Remove("url");
        }
        return document;
    }

    // ================================================================== DTR forms

    /// <summary>
    /// PUT {fhir}/QuestionnaireResponse/{id}: creates or replaces a DTR form. The Provider FHIR API stores it in Oracle
    /// and copies it to the EHR. Returns the stored resource.
    /// </summary>
    public async Task<JsonObject> SaveQuestionnaireResponseAsync(JsonObject questionnaireResponse, CancellationToken ct)
    {
        string? id = FhirJson.Id(questionnaireResponse);
        if (string.IsNullOrEmpty(id))
        {
            throw new FhirApiException("A QuestionnaireResponse needs an id before it can be saved.");
        }

        string url = GetBaseUrl() + "/QuestionnaireResponse/" + Uri.EscapeDataString(id);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Content = new StringContent(questionnaireResponse.ToJsonString(), Encoding.UTF8, ApiHeaders.FhirJson);
        request.Headers.Add("Accept", ApiHeaders.FhirJson);
        request.Headers.Add(ApiHeaders.InternalKey, _settings.InternalApiKey);

        string text = await SendAsync(request, "PUT QuestionnaireResponse/" + id, false, ct);
        JsonObject? saved = ParseObject(text);
        if (saved == null)
        {
            return questionnaireResponse;
        }
        return saved;
    }

    // ================================================================== /internal

    /// <summary>
    /// Short-lived token that lets the payer read this patient's data during CRD.
    /// Returned unchanged because it already has the CDS Hooks fhirAuthorization shape.
    /// </summary>
    public async Task<JsonObject?> IssueAccessTokenAsync(string payerName, string patientId, int minutes, CancellationToken ct)
    {
        JsonObject body = new JsonObject();
        body["subject"] = payerName;
        body["patientId"] = patientId;
        body["minutes"] = minutes;
        return await PostInternalAsync("internal/access-tokens", body, ct);
    }

    /// <summary>Creates a SMART launch for a payer DTR app. Returns the launch id and iss (FHIR base URL).</summary>
    public async Task<SmartLaunchResult> CreateSmartLaunchAsync(string patientId, string? userPractitionerId,
        string? encounterId, List<string> fhirContext, string? appContext, CancellationToken ct)
    {
        JsonArray contextArray = new JsonArray();
        foreach (string reference in fhirContext)
        {
            contextArray.Add(reference);
        }

        JsonObject body = new JsonObject();
        body["patientId"] = patientId;
        body["userPractitionerId"] = userPractitionerId;
        body["encounterId"] = encounterId;
        body["fhirContext"] = contextArray;
        body["appContext"] = appContext;

        JsonObject? result = await PostInternalAsync("internal/smart-launches", body, ct);
        string? launchId = FhirJson.GetString(result, "launchId");
        string? iss = FhirJson.GetString(result, "iss");
        if (launchId == null || iss == null)
        {
            throw new FhirApiException("The SMART launch response has no launchId / iss.");
        }

        SmartLaunchResult launch = new SmartLaunchResult();
        launch.LaunchId = launchId;
        launch.Iss = iss;
        return launch;
    }

    // ================================================================== plumbing

    /// <summary>GET with the internal key. 404 -> null; other errors -> FhirApiException.</summary>
    private async Task<JsonNode?> GetJsonAsync(string url, CancellationToken ct)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", ApiHeaders.FhirJson);
        request.Headers.Add(ApiHeaders.InternalKey, _settings.InternalApiKey);

        string text = await SendAsync(request, "GET " + url, true, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new FhirApiException("GET " + url + " did not return JSON.", ex);
        }
    }

    /// <summary>POST to {root}/internal/... with the internal key.</summary>
    private async Task<JsonObject?> PostInternalAsync(string path, JsonObject body, CancellationToken ct)
    {
        string url = _settings.InternalBaseUrl.TrimEnd('/') + "/" + path;
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, ApiHeaders.Json);
        request.Headers.Add(ApiHeaders.InternalKey, _settings.InternalApiKey);

        string text = await SendAsync(request, "POST " + path, false, ct);
        return ParseObject(text);
    }

    /// <summary>
    /// Sends with the configured timeout and returns the body text.
    /// notFoundIsEmpty = true: a 404 returns "" (resource does not exist). Other errors -> FhirApiException.
    /// </summary>
    private async Task<string> SendAsync(HttpRequestMessage request, string label, bool notFoundIsEmpty, CancellationToken ct)
    {
        using (request)
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));
            HttpClient http = _httpFactory.CreateClient();
            try
            {
                using (HttpResponseMessage response = await http.SendAsync(request, timeout.Token))
                {
                    if (notFoundIsEmpty && response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return string.Empty;
                    }
                    string text = await response.Content.ReadAsStringAsync(timeout.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new FhirApiException(label + " returned HTTP " + (int)response.StatusCode + ": " + TextHelper.Shorten(text, 300));
                    }
                    return text;
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                throw new FhirApiException(label + " did not answer within " + _settings.TimeoutSeconds + " seconds.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new FhirApiException(label + " failed: " + ex.Message, ex);
            }
        }
    }

    /// <summary>Text -> JSON object, or null when empty / not an object.</summary>
    private static JsonObject? ParseObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
