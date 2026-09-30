namespace PayerGateway.Api.Models.Fhir;

/// <summary>Claim.supportingInfo: DTR forms, documents or a free-text note sent with the request.</summary>
public class ClaimSupportingInfo
{
    public int Sequence { get; set; }
    public CodeableConcept? Category { get; set; }
    public string? ValueString { get; set; }
    public ResourceReference? ValueReference { get; set; }
}
