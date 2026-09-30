namespace PayerGateway.Api.Models.Responses;

/// <summary>How the payer call went (shown in the order-sign response for troubleshooting).</summary>
public class PayerCallSummary
{
    /// <example>MockPayer</example>
    public string PayerName { get; set; } = string.Empty;

    /// <summary>The payer's CDS service id used for order-sign.</summary>
    /// <example>order-sign-crd</example>
    public string? ServiceId { get; set; }

    /// <summary>ok | skipped | timeout | error</summary>
    /// <example>ok</example>
    public string Outcome { get; set; } = "ok";

    /// <example>412</example>
    public long DurationMs { get; set; }

    public string? Error { get; set; }
}
