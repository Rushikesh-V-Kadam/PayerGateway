namespace PayerGateway.Api.Models.Responses;

/// <summary>
/// The payer's prior authorization decision in plain JSON. Returned by every PAS endpoint and written to the EHR
/// (PUT api/prior-auth-requests/{requestId}).
/// </summary>
public class PriorAuthDecision
{
    /// <example>PA-2026-0001</example>
    public string RequestId { get; set; } = string.Empty;

    /// <example>MockPayer</example>
    public string? PayerName { get; set; }

    /// <example>pat-1001</example>
    public string PatientId { get; set; } = string.Empty;

    /// <example>["ord-1001"]</example>
    public List<string> OrderIds { get; set; } = new();

    /// <summary>approved | partially-approved | denied | pended | not-required | cancelled | contact-payer | error</summary>
    /// <example>pended</example>
    public string OverallStatus { get; set; } = "pended";

    /// <summary>Payer's authorization number (put it on the claim later). Also the CDex tracking reference.</summary>
    /// <example>PA-2026-000123</example>
    public string? AuthorizationNumber { get; set; }

    /// <example>2026-10-01</example>
    public string? ValidFrom { get; set; }

    /// <example>2027-03-31</example>
    public string? ValidTo { get; set; }

    public string? DecidedAt { get; set; }

    /// <summary>When the payer must decide at the latest (CMS-0057-F: 72 hours urgent, 7 days standard).</summary>
    /// <example>2026-10-06T07:30:00Z</example>
    public string? DecisionDueBy { get; set; }

    /// <summary>One decision per order / claim line.</summary>
    public List<PriorAuthLine> Lines { get; set; } = new();

    /// <summary>Documents / forms the payer asked for (CDex). Answer with POST /api/v1/cdex/requests/{id}/attachments.</summary>
    public List<InformationRequest> InformationRequests { get; set; } = new();

    /// <summary>Errors returned by the payer (technical rejection).</summary>
    /// <example>[]</example>
    public List<string> Errors { get; set; } = new();
}
