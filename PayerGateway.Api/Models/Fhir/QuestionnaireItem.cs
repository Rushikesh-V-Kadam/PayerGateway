namespace PayerGateway.Api.Models.Fhir;

/// <summary>One question or group of a Questionnaire.</summary>
public class QuestionnaireItem
{
    public string LinkId { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? Text { get; set; }
    /// <summary>group | display | boolean | decimal | integer | date | dateTime | time | string | text | url | choice | open-choice | attachment | reference | quantity</summary>
    public string Type { get; set; } = "string";
    public bool? Required { get; set; }
    public bool? Repeats { get; set; }
    public bool? ReadOnly { get; set; }
    public int? MaxLength { get; set; }
    /// <summary>all | any: how several enableWhen conditions combine.</summary>
    public string? EnableBehavior { get; set; }
    public List<QuestionnaireEnableWhen> EnableWhen { get; set; } = new();
    public List<QuestionnaireAnswerOption> AnswerOption { get; set; } = new();
    public string? AnswerValueSet { get; set; }
    public List<QuestionnaireInitial> Initial { get; set; } = new();
    public List<QuestionnaireItem> Item { get; set; } = new();
}
