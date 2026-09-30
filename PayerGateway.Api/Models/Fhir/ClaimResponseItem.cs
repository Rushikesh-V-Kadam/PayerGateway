namespace PayerGateway.Api.Models.Fhir;

/// <summary>Decision for one Claim.item. The decision itself is in the PAS reviewAction extension.</summary>
public class ClaimResponseItem
{
    public List<Extension> Extension { get; set; } = new();
    public int ItemSequence { get; set; }
    public List<int> NoteNumber { get; set; } = new();
}
