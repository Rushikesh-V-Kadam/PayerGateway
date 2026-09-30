namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Coding: one code from one code system (e.g. LOINC 18748-4).</summary>
public class Coding
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }

    /// <summary>Empty coding (used by the JSON serializer).</summary>
    public Coding() { }

    /// <summary>Creates a coding.</summary>
    public Coding(string? system, string? code, string? display = null) { System = system; Code = code; Display = display; }
}
