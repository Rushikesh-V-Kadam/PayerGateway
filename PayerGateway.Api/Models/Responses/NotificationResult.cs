namespace PayerGateway.Api.Models.Responses;

/// <summary>Response to a payer callback (PAS notification or pushed CDex Task).</summary>
public class NotificationResult
{
    /// <example>2 open request(s) checked</example>
    public string Status { get; set; } = string.Empty;

    /// <example>2</example>
    public int Count { get; set; }
}
