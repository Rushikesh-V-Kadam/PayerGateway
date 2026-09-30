using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.CdsHooks;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Services.Security;

namespace PayerGateway.Api.Services.Payers;

/// <summary>
/// Calls any payer's Da Vinci APIs. The same code serves every payer; only the PayerSettings differ
/// (base URL, auth, timeouts). Endpoints called:
///   CRD   GET  {cds}                  discovery          POST {cds}/{serviceId}         order-sign hook call
///   DTR   POST {fhir}/Questionnaire/$questionnaire-package, Questionnaire/$next-question
///   PAS   POST {fhir}/Claim/$submit, Claim/$inquire, Subscription
///   CDex  GET  {fhir}/Task?owner=...  POST {fhir}/$submit-attachment
/// </summary>
public class PayerClient
{
    /// <summary>OAuth tokens per payer, reused until 1 minute before they expire.</summary>
    private static readonly ConcurrentDictionary<string, CachedToken> Tokens = new ConcurrentDictionary<string, CachedToken>();

    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtSigner _signer;
    private readonly ILogger<PayerClient> _logger;

    /// <summary>Created by dependency injection.</summary>
    public PayerClient(IHttpClientFactory httpFactory, JwtSigner signer, ILogger<PayerClient> logger)
    {
        _httpFactory = httpFactory;
        _signer = signer;
        _logger = logger;
    }

    // ================================================================== CRD (CDS Hooks, plain JSON)

    /// <summary>GET {cds}: the payer's CDS services and prefetch templates.</summary>
    public async Task<CdsDiscoveryResponse> DiscoverAsync(PayerSettings payer, CancellationToken ct)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, payer.GetCdsServicesUrl());
        string text = await SendForTextAsync(payer, request, payer.TimeoutSeconds, ct);
        CdsDiscoveryResponse? discovery = ReadJson<CdsDiscoveryResponse>(payer, text);
        if (discovery == null)
        {
            return new CdsDiscoveryResponse();
        }
        return discovery;
    }

    /// <summary>POST {cds}/{serviceId}: the order-sign hook call. Uses the short CRD timeout.</summary>
    public async Task<CdsHookResponse> CallHookAsync(PayerSettings payer, string serviceId, CdsHookRequest hookRequest, CancellationToken ct)
    {
        string url = payer.GetCdsServicesUrl() + "/" + serviceId;
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(FhirJson.Serialize(hookRequest), Encoding.UTF8, ApiHeaders.Json);
        if (payer.SignCdsHooksRequests)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _signer.CreateCdsHooksJwt(url));
        }

        string text = await SendForTextAsync(payer, request, payer.CrdTimeoutSeconds, ct);
        CdsHookResponse? response = ReadJson<CdsHookResponse>(payer, text);
        if (response == null)
        {
            return new CdsHookResponse();
        }
        return response;
    }

    // ================================================================== FHIR operations (DTR, PAS, CDex)

    /// <summary>POST a FHIR body to {fhir}/{path}. The caller decides what a non-2xx result means.</summary>
    public async Task<PayerCallResult> PostFhirAsync(PayerSettings payer, string path, object body, CancellationToken ct)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, payer.GetFhirBaseUrl() + "/" + path);
        string json;
        JsonNode? node = body as JsonNode;
        if (node != null)
        {
            json = node.ToJsonString();
        }
        else
        {
            json = FhirJson.Serialize(body);
        }
        request.Content = new StringContent(json, Encoding.UTF8, ApiHeaders.FhirJson);
        return await SendFhirAsync(payer, request, ct);
    }

    /// <summary>GET {fhir}/{pathAndQuery}.</summary>
    public async Task<PayerCallResult> GetFhirAsync(PayerSettings payer, string pathAndQuery, CancellationToken ct)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, payer.GetFhirBaseUrl() + "/" + pathAndQuery);
        return await SendFhirAsync(payer, request, ct);
    }

    // ================================================================== plumbing

    /// <summary>Adds Accept + the payer token, sends, and parses the JSON body.</summary>
    private async Task<PayerCallResult> SendFhirAsync(PayerSettings payer, HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ApiHeaders.FhirJson));
        string? token = await GetTokenAsync(payer, ct);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        PayerCallResult result = new PayerCallResult();
        using (HttpResponseMessage response = await SendAsync(payer, request, payer.TimeoutSeconds, ct))
        {
            string text = await response.Content.ReadAsStringAsync(ct);
            result.StatusCode = (int)response.StatusCode;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    result.Body = JsonNode.Parse(text) as JsonObject;
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Payer {Payer} returned a body that is not JSON", payer.PayerName);
                }
            }
        }

        if (FhirJson.ResourceType(result.Body) == "OperationOutcome")
        {
            result.Outcome = FhirJson.Read<OperationOutcome>(result.Body);
        }
        return result;
    }

    /// <summary>Sends and returns the body text; a non-2xx status becomes PayerException (502).</summary>
    private async Task<string> SendForTextAsync(PayerSettings payer, HttpRequestMessage request, int timeoutSeconds, CancellationToken ct)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ApiHeaders.Json));
        string? url = request.RequestUri == null ? string.Empty : request.RequestUri.ToString();
        using (HttpResponseMessage response = await SendAsync(payer, request, timeoutSeconds, ct))
        {
            string text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new PayerException(payer.GetDisplayName() + " returned HTTP " + (int)response.StatusCode + " for " + url + ": " + TextHelper.Shorten(text, 300), 502);
            }
            return text;
        }
    }

    /// <summary>Sends with a timeout. Timeout -> PayerException 504, unreachable -> 502.</summary>
    private async Task<HttpResponseMessage> SendAsync(PayerSettings payer, HttpRequestMessage request, int timeoutSeconds, CancellationToken ct)
    {
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                HttpClient http = _httpFactory.CreateClient();
                HttpResponseMessage response = await http.SendAsync(request, timeout.Token);
                _logger.LogInformation("Payer {Payer} {Method} {Url} -> {Status} in {Ms} ms",
                    payer.PayerName, request.Method, request.RequestUri, (int)response.StatusCode, watch.ElapsedMilliseconds);
                return response;
            }
            catch (OperationCanceledException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                throw new PayerException(payer.GetDisplayName() + " did not answer within " + timeoutSeconds + " seconds.", 504, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new PayerException(payer.GetDisplayName() + " could not be reached: " + ex.Message, 502, ex);
            }
        }
    }

    /// <summary>
    /// OAuth token for FHIR calls (client_credentials). Auth type "none" -> no token.
    /// "client-secret" sends client id + secret; "smart-backend" sends a signed JWT client assertion.
    /// </summary>
    private async Task<string?> GetTokenAsync(PayerSettings payer, CancellationToken ct)
    {
        PayerAuthSettings auth = payer.Auth;
        if (auth.Type == "none" || string.IsNullOrEmpty(auth.TokenUrl) || string.IsNullOrEmpty(auth.ClientId))
        {
            return null;
        }

        CachedToken? cached;
        if (Tokens.TryGetValue(payer.PayerName, out cached) && cached.Expires > DateTime.UtcNow.AddMinutes(1))
        {
            return cached.Token;
        }

        Dictionary<string, string> form = new Dictionary<string, string>();
        form["grant_type"] = "client_credentials";
        form["scope"] = auth.Scope;
        if (auth.Type == "smart-backend")
        {
            form["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
            form["client_assertion"] = _signer.CreateClientAssertion(auth.ClientId, auth.TokenUrl);
        }
        else
        {
            form["client_id"] = auth.ClientId;
            form["client_secret"] = auth.ClientSecret ?? string.Empty;
        }

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, auth.TokenUrl);
        request.Content = new FormUrlEncodedContent(form);
        string text = await SendForTextAsync(payer, request, payer.TimeoutSeconds, ct);

        string? token = null;
        int seconds = 300;
        try
        {
            using (JsonDocument json = JsonDocument.Parse(text))
            {
                JsonElement value;
                if (json.RootElement.TryGetProperty("access_token", out value))
                {
                    token = value.GetString();
                }
                int expiresIn;
                if (json.RootElement.TryGetProperty("expires_in", out value) && value.TryGetInt32(out expiresIn))
                {
                    seconds = expiresIn;
                }
            }
        }
        catch (JsonException ex)
        {
            throw new PayerException(payer.GetDisplayName() + " token response is not JSON.", 502, ex);
        }
        if (string.IsNullOrEmpty(token))
        {
            throw new PayerException(payer.GetDisplayName() + " token response has no access_token.", 502);
        }

        CachedToken fresh = new CachedToken();
        fresh.Token = token;
        fresh.Expires = DateTime.UtcNow.AddSeconds(seconds);
        Tokens[payer.PayerName] = fresh;
        return token;
    }

    /// <summary>JSON text -> object; a payer answer that is not valid JSON becomes PayerException (502).</summary>
    private static T? ReadJson<T>(PayerSettings payer, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }
        try
        {
            return FhirJson.Deserialize<T>(text);
        }
        catch (JsonException ex)
        {
            throw new PayerException(payer.GetDisplayName() + " returned an answer that could not be read: " + ex.Message, 502, ex);
        }
    }
}
