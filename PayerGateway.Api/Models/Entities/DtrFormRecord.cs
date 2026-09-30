using PayerGateway.Api.Models.Requests;

namespace PayerGateway.Api.Models.Entities;

/// <summary>
/// One payer questionnaire fetched for an order (table PA_DTR_FORM). Keeps the Questionnaire so the gateway can
/// show it as simple questions and build a correct QuestionnaireResponse from plain answers.
/// </summary>
public class DtrFormRecord
{
    public string FormId { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string? CoverageId { get; set; }
    public List<string> OrderIds { get; set; } = new();
    public string QuestionnaireUrl { get; set; } = string.Empty;
    /// <summary>The payer Questionnaire (FHIR JSON).</summary>
    public string QuestionnaireJson { get; set; } = string.Empty;
    /// <summary>new | in-progress | completed</summary>
    public string Status { get; set; } = "new";
    /// <summary>Id of the QuestionnaireResponse saved at the Provider FHIR API.</summary>
    public string? QuestionnaireResponseId { get; set; }
    /// <summary>The latest plain answers (to show them again).</summary>
    public List<DtrAnswerInput> Answers { get; set; } = new();
    /// <summary>true = adaptive questionnaire: the payer adds questions with $next-question.</summary>
    public bool Adaptive { get; set; }
    /// <summary>Adaptive only: true when the payer said there are no more questions (status completed).</summary>
    public bool PayerFinished { get; set; }
    /// <summary>Adaptive only: the last QuestionnaireResponse returned by the payer's $next-question.</summary>
    public string? AdaptiveResponseJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
