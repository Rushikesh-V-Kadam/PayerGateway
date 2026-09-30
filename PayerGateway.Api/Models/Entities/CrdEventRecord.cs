namespace PayerGateway.Api.Models.Entities;

/// <summary>One order-sign event and the answer returned for it (table PA_CRD_EVENT). A repeated eventId gets the same answer.</summary>
public class CrdEventRecord
{
    public string EventId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    /// <summary>The OrderSignResponse as JSON.</summary>
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
