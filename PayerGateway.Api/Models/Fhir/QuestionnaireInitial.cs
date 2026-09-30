namespace PayerGateway.Api.Models.Fhir;

/// <summary>A default answer the payer put on a question.</summary>
public class QuestionnaireInitial
{
    public bool? ValueBoolean { get; set; }
    public decimal? ValueDecimal { get; set; }
    public int? ValueInteger { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueDateTime { get; set; }
    public string? ValueString { get; set; }
    public Coding? ValueCoding { get; set; }
    public Quantity? ValueQuantity { get; set; }
}
