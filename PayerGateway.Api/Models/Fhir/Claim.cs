namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR Claim (PAS profile-claim / profile-claim-update / profile-claim-inquiry) built by the gateway.
/// use = "preauthorization". One item per order.
/// </summary>
public class Claim
{
    public string ResourceType { get; set; } = "Claim";
    public string? Id { get; set; }
    public Meta? Meta { get; set; }
    public List<Identifier> Identifier { get; set; } = new();
    public string Status { get; set; } = "active";
    public CodeableConcept? Type { get; set; }
    public string Use { get; set; } = "preauthorization";
    public ResourceReference? Patient { get; set; }
    public string? Created { get; set; }
    public ResourceReference? Insurer { get; set; }
    public ResourceReference? Provider { get; set; }
    public CodeableConcept? Priority { get; set; }
    public List<ClaimRelated> Related { get; set; } = new();
    public List<ClaimCareTeam> CareTeam { get; set; } = new();
    public List<ClaimSupportingInfo> SupportingInfo { get; set; } = new();
    public List<ClaimDiagnosis> Diagnosis { get; set; } = new();
    public List<ClaimInsurance> Insurance { get; set; } = new();
    public List<ClaimItem> Item { get; set; } = new();
}
