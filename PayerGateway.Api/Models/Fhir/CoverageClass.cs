namespace PayerGateway.Api.Models.Fhir;

/// <summary>Plan / group of a Coverage.</summary>
public class CoverageClass
{
    public CodeableConcept? Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Name { get; set; }
}
