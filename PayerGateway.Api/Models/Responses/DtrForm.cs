namespace PayerGateway.Api.Models.Responses;

/// <summary>
/// One payer questionnaire as simple questions (no FHIR needed to show it).
/// Show the questions, then send the answers to POST /api/v1/dtr/forms/{formId}/answers.
/// </summary>
public class DtrForm
{
    /// <summary>Gateway id of this form. Use it to read the form and to send the answers.</summary>
    /// <example>frm-6f1c2a9e</example>
    public string FormId { get; set; } = string.Empty;

    /// <example>MockPayer</example>
    public string PayerName { get; set; } = string.Empty;

    /// <example>pat-1001</example>
    public string PatientId { get; set; } = string.Empty;

    /// <example>["ord-1001"]</example>
    public List<string> OrderIds { get; set; } = new();

    /// <example>cov-1001</example>
    public string? CoverageId { get; set; }

    /// <summary>Canonical URL of the payer Questionnaire.</summary>
    /// <example>http://localhost:5080/fhir/Questionnaire/dialysis-pa</example>
    public string QuestionnaireUrl { get; set; } = string.Empty;

    /// <example>Hemodialysis prior authorization</example>
    public string? Title { get; set; }

    /// <summary>new | in-progress | completed</summary>
    /// <example>new</example>
    public string Status { get; set; } = "new";

    /// <summary>Id of the saved QuestionnaireResponse at the Provider FHIR API (after answers were sent).</summary>
    /// <example>qr-frm-6f1c2a9e</example>
    public string? QuestionnaireResponseId { get; set; }

    /// <summary>true = adaptive questionnaire: the payer sends new questions after each set of answers.</summary>
    /// <example>false</example>
    public bool Adaptive { get; set; }

    /// <summary>Adaptive only: true while the payer may still add questions (send the answers to get them).</summary>
    /// <example>false</example>
    public bool MoreQuestionsExpected { get; set; }

    /// <summary>The questions in display order (groups are flattened; groupText tells which section a question is in).</summary>
    public List<DtrQuestion> Questions { get; set; } = new();
}
