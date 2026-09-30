namespace PayerGateway.Api.Models.Responses;

/// <summary>Response of POST /api/v1/dtr/forms/{formId}/answers.</summary>
public class DtrAnswersResult
{
    /// <example>frm-6f1c2a9e</example>
    public string FormId { get; set; } = string.Empty;

    /// <summary>in-progress | completed</summary>
    /// <example>completed</example>
    public string Status { get; set; } = "completed";

    /// <summary>Id of the QuestionnaireResponse saved at the Provider FHIR API. PAS attaches it automatically.</summary>
    /// <example>qr-frm-6f1c2a9e</example>
    public string QuestionnaireResponseId { get; set; } = string.Empty;

    /// <example>5</example>
    public int AnsweredCount { get; set; }

    /// <summary>Required questions still unanswered (only shown when saving as in-progress).</summary>
    /// <example>[]</example>
    public List<string> MissingRequired { get; set; } = new();

    /// <summary>true = adaptive questionnaire.</summary>
    /// <example>false</example>
    public bool Adaptive { get; set; }

    /// <summary>Adaptive only: questions the payer added after these answers. Read the form again and answer them.</summary>
    /// <example>0</example>
    public int NewQuestions { get; set; }

    /// <summary>Adaptive only: true while the payer may still add questions.</summary>
    /// <example>false</example>
    public bool MoreQuestionsExpected { get; set; }

    /// <summary>Answers that were ignored (unknown linkId, display-only question, unsupported type).</summary>
    /// <example>[]</example>
    public List<string> Warnings { get; set; } = new();
}
