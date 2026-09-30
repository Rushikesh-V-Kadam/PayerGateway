namespace PayerGateway.Api.Models.Fhir;

/// <summary>The file of a DocumentReference.</summary>
public class DocumentReferenceContent
{
    public Attachment Attachment { get; set; } = new();
}
