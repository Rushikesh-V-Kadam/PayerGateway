namespace PayerGateway.Api.Models.Responses;

/// <summary>"Show this question when question X operator value" (from the Questionnaire enableWhen).</summary>
public class DtrCondition
{
    /// <summary>linkId of the question to look at.</summary>
    /// <example>on-dialysis</example>
    public string QuestionLinkId { get; set; } = string.Empty;

    /// <summary>exists | = | != | &gt; | &lt; | &gt;= | &lt;=</summary>
    /// <example>=</example>
    public string Operator { get; set; } = "=";

    /// <summary>Value to compare with (for "exists": "true" or "false").</summary>
    /// <example>true</example>
    public string Value { get; set; } = string.Empty;
}
