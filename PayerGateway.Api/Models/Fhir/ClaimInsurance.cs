namespace PayerGateway.Api.Models.Fhir;

/// <summary>Claim.insurance: the Coverage the request is for.</summary>
public class ClaimInsurance
{
    public int Sequence { get; set; } = 1;
    public bool Focal { get; set; } = true;
    public ResourceReference? Coverage { get; set; }
}
