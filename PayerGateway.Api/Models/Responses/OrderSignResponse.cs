namespace PayerGateway.Api.Models.Responses;

/// <summary>Response of POST /api/v1/crd/order-sign: one coverage decision per signed order.</summary>
public class OrderSignResponse
{
    /// <example>evt-20260929-0001</example>
    public string EventId { get; set; } = string.Empty;

    /// <example>MockPayer</example>
    public string PayerName { get; set; } = string.Empty;

    /// <summary>One decision per order. Read callDtr / callPas to know what to do next.</summary>
    public List<CoverageDecision> Decisions { get; set; } = new();

    /// <summary>Messages not tied to one order (order not found, general payer cards).</summary>
    public List<EhrMessage> Messages { get; set; } = new();

    /// <summary>How the payer call went: outcome and duration (for support).</summary>
    public PayerCallSummary PayerCall { get; set; } = new();
}
