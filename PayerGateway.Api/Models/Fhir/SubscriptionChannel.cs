using System.Text.Json.Serialization;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>Where and how the payer sends notifications.</summary>
public class SubscriptionChannel
{
    public string Type { get; set; } = "rest-hook";
    public string? Endpoint { get; set; }
    public string? Payload { get; set; }

    /// <summary>Holds backport-payload-content (id-only).</summary>
    [JsonPropertyName("_payload")]
    public PrimitiveExtensions? PayloadExtensions { get; set; }

    /// <summary>HTTP headers the payer adds to each notification (e.g. our shared secret).</summary>
    public List<string> Header { get; set; } = new();
}
