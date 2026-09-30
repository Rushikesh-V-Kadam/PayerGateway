namespace PayerGateway.Api.Models.Fhir;

/// <summary>Claim.related: an earlier authorization this request renews (relationship "prior", reference = its number).</summary>
public class ClaimRelated
{
    public CodeableConcept? Relationship { get; set; }
    public Identifier? Reference { get; set; }
}
