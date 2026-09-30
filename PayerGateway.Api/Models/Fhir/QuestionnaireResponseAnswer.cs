namespace PayerGateway.Api.Models.Fhir;

/// <summary>One answer value. Only one value[x] is set, chosen by the question type.</summary>
public class QuestionnaireResponseAnswer
{
    /// <summary>information-origin extension (auto / override / manual).</summary>
    public List<Extension> Extension { get; set; } = new();
    public bool? ValueBoolean { get; set; }
    public decimal? ValueDecimal { get; set; }
    public int? ValueInteger { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueDateTime { get; set; }
    public string? ValueTime { get; set; }
    public string? ValueString { get; set; }
    public string? ValueUri { get; set; }
    public Coding? ValueCoding { get; set; }
    public Quantity? ValueQuantity { get; set; }
    /// <summary>Child items of a question that has sub-questions (they sit under the answer).</summary>
    public List<QuestionnaireResponseItem> Item { get; set; } = new();
}
