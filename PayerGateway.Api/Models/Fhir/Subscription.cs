using System.Text.Json.Serialization;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR R4 Subscription (Subscriptions R5 Backport, PAS profile-subscription): asks the payer to notify the gateway
/// when a pended decision changes.
/// </summary>
public class Subscription
{
    public string ResourceType { get; set; } = "Subscription";
    public string? Id { get; set; }
    public Meta? Meta { get; set; }
    public string Status { get; set; } = "requested";
    public string? End { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>The PAS SubscriptionTopic canonical.</summary>
    public string Criteria { get; set; } = string.Empty;

    /// <summary>Holds backport-filter-criteria (which organization's ClaimResponses).</summary>
    [JsonPropertyName("_criteria")]
    public PrimitiveExtensions? CriteriaExtensions { get; set; }

    public SubscriptionChannel Channel { get; set; } = new();
}
