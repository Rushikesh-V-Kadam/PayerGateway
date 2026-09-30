namespace PayerGateway.Api.Models.Responses;

/// <summary>
/// CRD answer for one order. Also written to the EHR (PUT api/orders/{orderId}/coverage-decision).
/// What to do next:
///   callDtr = true  -> DTR: POST /api/v1/dtr/forms with questionnaireUrls, then POST .../answers
///   callPas = true  -> PAS: POST /api/v1/pas/requests (after DTR when both are true)
///   both false      -> nothing more for this order (see covered / priorAuthNeeded)
/// </summary>
public class CoverageDecision
{
    /// <example>ord-1001</example>
    public string OrderId { get; set; } = string.Empty;

    /// <example>MockPayer</example>
    public string? PayerName { get; set; }

    /// <example>cov-1001</example>
    public string? CoverageId { get; set; }

    /// <summary>covered | not-covered | conditional | unknown (unknown = the payer could not be asked).</summary>
    /// <example>covered</example>
    public string Covered { get; set; } = "unknown";

    /// <summary>no-auth | auth-needed | satisfied | performpa | conditional (payer "pa-needed").</summary>
    /// <example>auth-needed</example>
    public string? PriorAuthNeeded { get; set; }

    /// <summary>no-doc | clinical | admin | both | conditional (payer "doc-needed").</summary>
    /// <example>clinical</example>
    public string? DocumentationNeeded { get; set; }

    /// <summary>true = fill in the payer's questionnaire (DTR) before submitting.</summary>
    /// <example>true</example>
    public bool CallDtr { get; set; }

    /// <summary>true = submit a prior authorization request (PAS).</summary>
    /// <example>true</example>
    public bool CallPas { get; set; }

    /// <summary>withpa | withclaim | withorder | retain-doc</summary>
    /// <example>["withpa"]</example>
    public List<string> DocumentationPurpose { get; set; } = new();

    /// <summary>performer | location | timeframe | contract-window (information the payer needs to decide).</summary>
    /// <example>[]</example>
    public List<string> InformationNeeded { get; set; } = new();

    /// <summary>Payer questionnaires to fill in (pass them to POST /api/v1/dtr/forms).</summary>
    /// <example>["http://localhost:5080/fhir/Questionnaire/dialysis-pa"]</example>
    public List<string> QuestionnaireUrls { get; set; } = new();

    /// <summary>Payer id of this CRD answer. Pass it to DTR as coverageAssertionId.</summary>
    /// <example>CA-20260929-0001</example>
    public string? CoverageAssertionId { get; set; }

    /// <summary>Existing authorization that already covers the order (priorAuthNeeded = satisfied).</summary>
    public string? SatisfiedAuthorizationNumber { get; set; }

    /// <summary>Payer cards and reasons. A SMART (DTR app) link appears here when the payer offers one.</summary>
    public List<EhrMessage> Messages { get; set; } = new();

    /// <example>2026-09-29T07:30:00Z</example>
    public string? DecidedAt { get; set; }
}
