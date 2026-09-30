using System.Text.Json.Nodes;

namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>A systemAction. CRD uses type "update" with the order carrying the coverage-information extension.</summary>
public class CdsAction
{
    public string Type { get; set; } = "update";
    public string? Description { get; set; }
    /// <summary>The updated order as raw FHIR JSON.</summary>
    public JsonObject? Resource { get; set; }
}
