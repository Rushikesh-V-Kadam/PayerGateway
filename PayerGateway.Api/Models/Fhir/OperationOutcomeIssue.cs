namespace PayerGateway.Api.Models.Fhir;

/// <summary>One issue of an OperationOutcome.</summary>
public class OperationOutcomeIssue
{
    public string Severity { get; set; } = "error";
    public string Code { get; set; } = "processing";
    public string? Diagnostics { get; set; }
    public List<string> Expression { get; set; } = new();
}
