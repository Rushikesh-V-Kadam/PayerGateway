namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// One Task input: attachments-needed (LOINC), questionnaires-needed (canonical), payer-url ...
/// The claim line it applies to is in an extension (paLineNumber / serviceLineNumber).
/// </summary>
public class TaskInput
{
    public List<Extension> Extension { get; set; } = new();
    public CodeableConcept Type { get; set; } = new();
    public string? ValueString { get; set; }
    public string? ValueUrl { get; set; }
    public string? ValueCanonical { get; set; }
    public Identifier? ValueIdentifier { get; set; }
    public CodeableConcept? ValueCodeableConcept { get; set; }
}
