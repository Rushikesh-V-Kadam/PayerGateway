namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Identifier: a business id (NPI, member id, tracking id...).</summary>
public class Identifier
{
    public CodeableConcept? Type { get; set; }
    public string? System { get; set; }
    public string? Value { get; set; }

    /// <summary>Empty identifier (used by the JSON serializer).</summary>
    public Identifier() { }

    /// <summary>Creates an identifier.</summary>
    public Identifier(string? system, string? value) { System = system; Value = value; }
}
