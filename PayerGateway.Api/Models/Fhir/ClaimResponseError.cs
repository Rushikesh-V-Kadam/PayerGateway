namespace PayerGateway.Api.Models.Fhir;

/// <summary>A processing error on the request or on one item.</summary>
public class ClaimResponseError
{
    public int? ItemSequence { get; set; }
    public CodeableConcept Code { get; set; } = new();
}
