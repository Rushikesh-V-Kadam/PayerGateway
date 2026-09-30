using System.ComponentModel.DataAnnotations;

namespace PayerGateway.Api.Models.Requests;

/// <summary>
/// Body of POST /api/v1/dtr/forms/{formId}/answers: the provider's answers in plain JSON.
/// The gateway builds the FHIR QuestionnaireResponse (same tree as the payer's Questionnaire) and saves it at the
/// Provider FHIR API, where PAS picks it up. Can be sent several times; the latest answers replace the earlier ones.
/// </summary>
public class DtrAnswersRequest
{
    /// <summary>One entry per answered question. Questions not listed stay unanswered.</summary>
    [Required]
    public List<DtrAnswerInput> Answers { get; set; } = new();

    /// <summary>
    /// true = the form is finished (status "completed"): every required question must be answered, otherwise 422.
    /// false = save as "in-progress" and finish later.
    /// </summary>
    /// <example>true</example>
    public bool Complete { get; set; } = true;

    /// <summary>Clinician who answered (QuestionnaireResponse.author).</summary>
    /// <example>prac-2001</example>
    public string? AuthorPractitionerId { get; set; }
}
