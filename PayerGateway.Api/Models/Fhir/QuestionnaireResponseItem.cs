namespace PayerGateway.Api.Models.Fhir;

/// <summary>One question (or group) of a QuestionnaireResponse, in the same tree shape as the Questionnaire.</summary>
public class QuestionnaireResponseItem
{
    public string LinkId { get; set; } = string.Empty;
    public string? Text { get; set; }
    public List<QuestionnaireResponseAnswer> Answer { get; set; } = new();
    /// <summary>Child items (for groups).</summary>
    public List<QuestionnaireResponseItem> Item { get; set; } = new();
}
