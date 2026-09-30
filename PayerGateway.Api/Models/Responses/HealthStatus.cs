namespace PayerGateway.Api.Models.Responses;

/// <summary>Response of GET /health.</summary>
public class HealthStatus
{
    /// <example>ok</example>
    public string Status { get; set; } = "ok";

    /// <example>2026-09-29T07:30:00Z</example>
    public string Time { get; set; } = string.Empty;
}
