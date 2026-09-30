namespace PayerGateway.Api.Models.Responses;

/// <summary>A message for the clinician, built from a payer card or a coverage-information reason.</summary>
public class EhrMessage
{
    /// <summary>info | warning | critical</summary>
    /// <example>info</example>
    public string Severity { get; set; } = "info";

    /// <example>Prior authorization required; clinical documentation needed</example>
    public string Summary { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public EhrMessageLink? Link { get; set; }
}
