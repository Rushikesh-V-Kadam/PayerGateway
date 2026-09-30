namespace PayerGateway.Api.Models.Responses;

/// <summary>A document type the payer needs, as a LOINC code.</summary>
public class DocumentNeeded
{
    /// <example>11506-3</example>
    public string LoincCode { get; set; } = string.Empty;

    /// <example>Progress note</example>
    public string? Display { get; set; }

    /// <summary>Claim line it is for, if the payer said.</summary>
    /// <example>1</example>
    public int? LineNumber { get; set; }
}
