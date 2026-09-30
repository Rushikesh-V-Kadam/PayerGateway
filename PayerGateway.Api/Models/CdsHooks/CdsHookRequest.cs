using System.Text.Json.Nodes;

namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>Body of POST {payer}/cds-services/{id}: the CDS Hooks call the gateway makes for the EHR.</summary>
public class CdsHookRequest
{
    public string Hook { get; set; } = string.Empty;
    public string HookInstance { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Provider FHIR API base URL the payer may query.</summary>
    public string? FhirServer { get; set; }
    /// <summary>Token for FhirServer (from the Provider FHIR API /internal/access-tokens).</summary>
    public JsonObject? FhirAuthorization { get; set; }
    /// <summary>Hook context: patientId, userId, draftOrders ...</summary>
    public JsonObject Context { get; set; } = new();
    /// <summary>Data the payer asked for in advance, filled from the Provider FHIR API.</summary>
    public Dictionary<string, JsonNode?>? Prefetch { get; set; }
    /// <summary>davinci-crd.configuration (options such as max-cards).</summary>
    public JsonObject? Extension { get; set; }
}
