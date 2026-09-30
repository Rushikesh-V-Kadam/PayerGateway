namespace PayerGateway.Api.Models.Fhir;

/// <summary>Claim.item: one requested service (one order). Sequence numbers stay the same across updates.</summary>
public class ClaimItem
{
    public List<Extension> Extension { get; set; } = new();
    public List<Extension> ModifierExtension { get; set; } = new();
    public int Sequence { get; set; }
    public List<int> CareTeamSequence { get; set; } = new();
    public List<int> DiagnosisSequence { get; set; } = new();
    public List<int> InformationSequence { get; set; } = new();
    /// <summary>X12 1365 service type.</summary>
    public CodeableConcept? Category { get; set; }
    public CodeableConcept? ProductOrService { get; set; }
    public string? ServicedDate { get; set; }
    public Period? ServicedPeriod { get; set; }
    public CodeableConcept? LocationCodeableConcept { get; set; }
    public Quantity? Quantity { get; set; }
}
