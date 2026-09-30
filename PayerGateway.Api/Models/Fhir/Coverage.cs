namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR Coverage (CRD profile) read from the Provider FHIR API. Fully modelled because the gateway changes it
/// before sending it to a payer (payer-specific identifier systems).
/// </summary>
public class Coverage
{
    public string ResourceType { get; set; } = "Coverage";
    public string? Id { get; set; }
    public Meta? Meta { get; set; }
    public List<Identifier> Identifier { get; set; } = new();
    public string? Status { get; set; }
    public CodeableConcept? Type { get; set; }
    public string? SubscriberId { get; set; }
    public ResourceReference? Beneficiary { get; set; }
    public CodeableConcept? Relationship { get; set; }
    public Period? Period { get; set; }
    /// <summary>The payer organization (display name and identifier).</summary>
    public List<ResourceReference> Payor { get; set; } = new();
    public List<CoverageClass> Class { get; set; } = new();

    /// <summary>Member id: subscriberId, else the first identifier value.</summary>
    public string? MemberId()
    {
        if (!string.IsNullOrEmpty(SubscriberId))
        {
            return SubscriberId;
        }
        if (Identifier.Count > 0)
        {
            return Identifier[0].Value;
        }
        return null;
    }

    /// <summary>Display name of the first payor, or null.</summary>
    public string? PayorDisplay()
    {
        if (Payor.Count == 0)
        {
            return null;
        }
        return Payor[0].Display;
    }
}
