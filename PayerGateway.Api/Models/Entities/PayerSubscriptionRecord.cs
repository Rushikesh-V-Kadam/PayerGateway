namespace PayerGateway.Api.Models.Entities;

/// <summary>The PAS Subscription created at a payer for one provider organization (table PA_PAYER_SUBSCRIPTION).</summary>
public class PayerSubscriptionRecord
{
    public string PayerName { get; set; } = string.Empty;
    public string OrganizationNpi { get; set; } = string.Empty;
    public string? SubscriptionId { get; set; }
    /// <summary>active | error</summary>
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
