namespace PayerGateway.Api.Models.Fhir;

/// <summary>Claim.diagnosis: an ICD-10 code taken from the orders.</summary>
public class ClaimDiagnosis
{
    public int Sequence { get; set; }
    public CodeableConcept? DiagnosisCodeableConcept { get; set; }
}
