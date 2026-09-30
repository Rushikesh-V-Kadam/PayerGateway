namespace PayerGateway.Api.Models.Entities;

/// <summary>A payer information request (CDex / PAS Task) received for one of our requests (table PA_CDEX_TASK).</summary>
public class CdexTaskRecord
{
    public string PayerName { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string TrackingId { get; set; } = string.Empty;
    /// <summary>System of the tracking id; sent back unchanged in $submit-attachment.</summary>
    public string? TrackingSystem { get; set; }
    public string? Status { get; set; }
    public string? DueDate { get; set; }
    /// <summary>The Task as received (FHIR JSON).</summary>
    public string TaskJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
