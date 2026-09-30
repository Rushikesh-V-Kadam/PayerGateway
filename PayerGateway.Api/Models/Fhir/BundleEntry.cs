using System.Text.Json.Nodes;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>One entry of a Bundle.</summary>
public class BundleEntry
{
    public string? FullUrl { get; set; }

    /// <summary>The resource as raw JSON.</summary>
    public JsonObject? Resource { get; set; }
}
