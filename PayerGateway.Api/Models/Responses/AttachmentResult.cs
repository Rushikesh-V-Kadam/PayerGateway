namespace PayerGateway.Api.Models.Responses;

/// <summary>Response of POST /api/v1/cdex/requests/{requestId}/attachments.</summary>
public class AttachmentResult
{
    /// <example>PA-2026-0001</example>
    public string RequestId { get; set; } = string.Empty;

    /// <example>TRK-88421</example>
    public string TrackingId { get; set; } = string.Empty;

    /// <summary>sent | rejected</summary>
    /// <example>sent</example>
    public string Status { get; set; } = "sent";

    /// <example>1</example>
    public int AttachmentCount { get; set; }

    /// <summary>Messages from the payer's OperationOutcome.</summary>
    /// <example>[]</example>
    public List<string> Messages { get; set; } = new();

    /// <summary>Latest decision after the payer re-reviewed (null when the payer has not answered yet).</summary>
    public PriorAuthDecision? Decision { get; set; }
}
