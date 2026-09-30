namespace PayerGateway.Api.Models.Responses;

/// <summary>One allowed answer of a choice question.</summary>
public class DtrOption
{
    /// <summary>Send this value as the answer.</summary>
    /// <example>in-center</example>
    public string Code { get; set; } = string.Empty;

    /// <example>In-center hemodialysis</example>
    public string Display { get; set; } = string.Empty;

    /// <summary>Code system of the option (null for plain text options).</summary>
    /// <example>http://payer.example.org/codes</example>
    public string? System { get; set; }
}
