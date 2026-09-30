namespace PayerGateway.Api.Models.Requests;

/// <summary>Body of POST /api/v1/pas/requests/{requestId}/cancel (optional).</summary>
public class CancelPriorAuthRequest
{
    /// <summary>Why the request is cancelled (sent to the payer as a note).</summary>
    /// <example>Order cancelled by the physician.</example>
    public string? Reason { get; set; }

    /// <summary>true (default) = also PUT the cancelled decision to the EHR.</summary>
    /// <example>true</example>
    public bool WriteBack { get; set; } = true;
}
