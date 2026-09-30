namespace PayerGateway.Api.Models.Fhir;

/// <summary>Claim.careTeam: the requesting practitioner.</summary>
public class ClaimCareTeam
{
    public int Sequence { get; set; }
    public ResourceReference? Provider { get; set; }
    public CodeableConcept? Role { get; set; }
}
