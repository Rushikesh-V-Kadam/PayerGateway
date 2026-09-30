namespace PayerGateway.Api.Models.Entities;

/// <summary>Latest CRD decision of an order (table PA_COVERAGE_DECISION).</summary>
public class CoverageDecisionRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string? PayerName { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string? CoverageAssertionId { get; set; }
    public string Covered { get; set; } = string.Empty;
    public string? PriorAuthNeeded { get; set; }
    public string? DocumentationNeeded { get; set; }
    /// <summary>The CoverageDecision as JSON.</summary>
    public string DecisionJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
