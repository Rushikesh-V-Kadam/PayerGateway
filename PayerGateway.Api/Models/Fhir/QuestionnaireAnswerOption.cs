namespace PayerGateway.Api.Models.Fhir;

/// <summary>One allowed answer of a choice question.</summary>
public class QuestionnaireAnswerOption
{
    public Coding? ValueCoding { get; set; }
    public string? ValueString { get; set; }
    public int? ValueInteger { get; set; }
    public string? ValueDate { get; set; }
    public bool? InitialSelected { get; set; }
}
