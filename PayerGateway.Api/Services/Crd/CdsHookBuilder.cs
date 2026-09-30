using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PayerGateway.Api.Clients;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Requests;

namespace PayerGateway.Api.Services.Crd;

/// <summary>
/// Builds the two data parts of the CDS Hooks order-sign request:
///  - context: patientId, userId, encounterId and draftOrders (a Bundle of the signed orders), per CRD 2.0.1
///  - prefetch: the payer's prefetch templates (from discovery) filled in and run against the Provider FHIR API
/// </summary>
public class CdsHookBuilder
{
    /// <summary>Finds "{{token}}" placeholders in prefetch templates.</summary>
    private static readonly Regex Placeholder = new Regex(@"\{\{\s*([^}\s]+)\s*\}\}", RegexOptions.Compiled);

    private readonly FhirApiClient _fhir;
    private readonly ILogger<CdsHookBuilder> _logger;

    /// <summary>Created by dependency injection.</summary>
    public CdsHookBuilder(FhirApiClient fhir, ILogger<CdsHookBuilder> logger)
    {
        _fhir = fhir;
        _logger = logger;
    }

    /// <summary>The order-sign context for these orders.</summary>
    public JsonObject BuildContext(OrderSignRequest request, List<JsonObject> orders, string? userPractitionerId)
    {
        JsonObject context = new JsonObject();
        if (!string.IsNullOrEmpty(userPractitionerId))
        {
            context["userId"] = "Practitioner/" + userPractitionerId;
        }
        context["patientId"] = request.PatientId;
        if (!string.IsNullOrEmpty(request.EncounterId))
        {
            context["encounterId"] = request.EncounterId;
        }
        context["draftOrders"] = CollectionBundle(orders);
        return context;
    }

    /// <summary>
    /// Fills each prefetch template and runs it against the Provider FHIR API.
    /// A template that cannot be filled, or whose query fails, is left out (CDS Hooks allows this;
    /// the payer can still query with fhirAuthorization).
    /// </summary>
    public async Task<Dictionary<string, JsonNode?>> BuildPrefetchAsync(Dictionary<string, string> templates,
        OrderSignRequest request, List<JsonObject> orders, string? userPractitionerId, CancellationToken ct)
    {
        Dictionary<string, string> values = PlaceholderValues(request, orders, userPractitionerId);
        Dictionary<string, JsonNode?> prefetch = new Dictionary<string, JsonNode?>();

        foreach (KeyValuePair<string, string> template in templates)
        {
            string? query = FillTemplate(template.Value, values);
            if (query == null)
            {
                _logger.LogInformation("Prefetch {Key} skipped: a placeholder has no value", template.Key);
                continue;
            }

            try
            {
                JsonNode? result = await _fhir.PrefetchAsync(query, ct);
                if (result != null)
                {
                    prefetch[template.Key] = result;
                }
            }
            catch (FhirApiException ex)
            {
                _logger.LogInformation("Prefetch {Key} ({Query}) skipped: {Error}", template.Key, query, ex.Message);
            }
        }
        return prefetch;
    }

    /// <summary>Replaces every {{token}} with its value. Null when a token has no value.</summary>
    private static string? FillTemplate(string template, Dictionary<string, string> values)
    {
        StringBuilder result = new StringBuilder();
        int position = 0;
        foreach (Match match in Placeholder.Matches(template))
        {
            string? value;
            if (!values.TryGetValue(match.Groups[1].Value, out value) || string.IsNullOrEmpty(value))
            {
                return null;
            }
            result.Append(template, position, match.Index - position);
            result.Append(value);
            position = match.Index + match.Length;
        }
        result.Append(template, position, template.Length - position);
        return result.ToString();
    }

    /// <summary>
    /// Values for the placeholders payers use, e.g. {{context.patientId}}, {{context.userId}},
    /// {{context.draftOrders.ServiceRequest.id}}, {{userPractitionerId}}.
    /// </summary>
    private static Dictionary<string, string> PlaceholderValues(OrderSignRequest request, List<JsonObject> orders, string? userPractitionerId)
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        values["context.patientId"] = request.PatientId;
        if (!string.IsNullOrEmpty(request.EncounterId))
        {
            values["context.encounterId"] = request.EncounterId;
        }
        if (!string.IsNullOrEmpty(userPractitionerId))
        {
            values["context.userId"] = "Practitioner/" + userPractitionerId;
            values["userPractitionerId"] = userPractitionerId;
        }

        // ids per resource type, comma separated: ServiceRequest -> "ord-1001,ord-1002"
        Dictionary<string, List<string>> idsByType = new Dictionary<string, List<string>>();
        foreach (JsonObject raw in orders)
        {
            string? type = FhirJson.ResourceType(raw);
            string? id = FhirJson.Id(raw);
            if (type == null || id == null)
            {
                continue;
            }
            if (!idsByType.ContainsKey(type))
            {
                idsByType[type] = new List<string>();
            }
            idsByType[type].Add(id);
        }
        foreach (KeyValuePair<string, List<string>> group in idsByType)
        {
            values["context.draftOrders." + group.Key + ".id"] = string.Join(",", group.Value);
        }
        return values;
    }

    /// <summary>A FHIR "collection" Bundle holding the orders (draftOrders).</summary>
    private static JsonObject CollectionBundle(List<JsonObject> orders)
    {
        JsonArray entries = new JsonArray();
        foreach (JsonObject order in orders)
        {
            JsonObject entry = new JsonObject();
            entry["resource"] = FhirJson.Copy(order);
            entries.Add(entry);
        }
        JsonObject bundle = new JsonObject();
        bundle["resourceType"] = "Bundle";
        bundle["type"] = "collection";
        bundle["entry"] = entries;
        return bundle;
    }
}
