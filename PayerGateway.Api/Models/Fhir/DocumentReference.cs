namespace PayerGateway.Api.Models.Fhir;

/// <summary>Typed view of a DocumentReference (type and file location).</summary>
public class DocumentReference
{
    public string ResourceType { get; set; } = "DocumentReference";
    public string? Id { get; set; }
    public CodeableConcept? Type { get; set; }
    public string? Date { get; set; }
    public List<DocumentReferenceContent> Content { get; set; } = new();
}
