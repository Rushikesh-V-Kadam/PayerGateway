using System.ComponentModel.DataAnnotations;

namespace PayerGateway.Api.Models.Requests;

/// <summary>
/// Body of POST /api/v1/dtr/forms: get the payer's questionnaire(s) for these orders as simple questions.
/// Use it when the order-sign decision has callDtr = true (or a CDex information request lists questionnaires).
/// </summary>
public class DtrFormRequest
{
    /// <summary>Payer to ask, as configured in appsettings "Payers".</summary>
    /// <example>MockPayer</example>
    [Required]
    public string PayerName { get; set; } = string.Empty;

    /// <summary>EHR patient id.</summary>
    /// <example>pat-1001</example>
    [Required]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Orders the documentation is for.</summary>
    /// <example>["ord-1001"]</example>
    [Required]
    public List<string> OrderIds { get; set; } = new();

    /// <summary>Insurance to use. Empty = the order's insurance, else the patient's first active coverage.</summary>
    /// <example>cov-1001</example>
    public string? CoverageId { get; set; }

    /// <summary>Questionnaire URLs from the order-sign decision (questionnaireUrls). Empty = the payer decides from the orders.</summary>
    /// <example>["http://localhost:5080/fhir/Questionnaire/dialysis-pa"]</example>
    public List<string> QuestionnaireUrls { get; set; } = new();

    /// <summary>coverageAssertionId from the order-sign decision (DTR "context"), or a CDex tracking id.</summary>
    /// <example>CA-20260929-0001</example>
    public string? CoverageAssertionId { get; set; }
}
