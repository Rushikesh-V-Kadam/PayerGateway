namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR OperationOutcome: errors / warnings returned by a payer.</summary>
public class OperationOutcome
{
    public string ResourceType { get; set; } = "OperationOutcome";
    public List<OperationOutcomeIssue> Issue { get; set; } = new();

    /// <summary>Human-readable messages of all issues.</summary>
    public List<string> Messages()
    {
        List<string> messages = new List<string>();
        foreach (OperationOutcomeIssue issue in Issue)
        {
            string text = issue.Diagnostics ?? issue.Code;
            if (issue.Expression.Count > 0)
            {
                text = text + " (" + string.Join(", ", issue.Expression) + ")";
            }
            messages.Add(text);
        }
        return messages;
    }
}
