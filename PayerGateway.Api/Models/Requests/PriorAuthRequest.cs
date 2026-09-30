using System.ComponentModel.DataAnnotations;

namespace PayerGateway.Api.Models.Requests;

/// <summary>
/// Body of POST /api/v1/pas/requests (submit) and PUT /api/v1/pas/requests/{requestId} (update).
/// The gateway turns it into a PAS request Bundle and calls the payer's Claim/$submit.
/// Completed DTR forms for these orders are attached automatically.
/// </summary>
public class PriorAuthRequest
{
    /// <summary>EHR id of the request. Becomes Claim.identifier and is used for update, cancel, inquire and CDex. Required for POST; may be empty on PUT (taken from the URL).</summary>
    /// <example>PA-2026-0001</example>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Payer to send the request to, as configured in appsettings "Payers".</summary>
    /// <example>MockPayer</example>
    [Required]
    public string PayerName { get; set; } = string.Empty;

    /// <summary>EHR patient id.</summary>
    /// <example>pat-1001</example>
    [Required]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Insurance to use. Empty = the orders' insurance, else the patient's first active coverage.</summary>
    /// <example>cov-1001</example>
    public string? CoverageId { get; set; }

    /// <summary>Provider organization asking for the authorization (must have an NPI).</summary>
    /// <example>org-3001</example>
    [Required]
    public string RequestingOrganizationId { get; set; } = string.Empty;

    /// <summary>Requesting clinician. Empty = the first order's ordering practitioner.</summary>
    /// <example>prac-2001</example>
    public string? RequestingPractitionerId { get; set; }

    /// <summary>Orders to authorize - one claim line each.</summary>
    /// <example>["ord-1001"]</example>
    [Required]
    public List<string> OrderIds { get; set; } = new();

    /// <summary>routine | urgent (urgent = expedited review, 72 hours under CMS-0057-F).</summary>
    /// <example>routine</example>
    public string Priority { get; set; } = "routine";

    /// <summary>new (default) | renewal. Renewal lines are sent with X12 certification type R and need previousAuthorizationNumber.</summary>
    /// <example>new</example>
    public string RequestType { get; set; } = "new";

    /// <summary>Authorization number being renewed (sent as Claim.related, relationship "prior").</summary>
    /// <example>PA20260706123456</example>
    public string? PreviousAuthorizationNumber { get; set; }

    /// <summary>First date of service of the previous authorization (yyyy-MM-dd).</summary>
    /// <example>2026-07-06</example>
    public string? PreviousServiceStart { get; set; }

    /// <summary>Last date of service of the previous authorization (yyyy-MM-dd).</summary>
    /// <example>2026-10-04</example>
    public string? PreviousServiceEnd { get; set; }

    /// <summary>Number of treatments given under the previous authorization.</summary>
    /// <example>39</example>
    public int? PreviousTreatments { get; set; }

    /// <summary>permanent (default) | transient (patient visiting the facility, e.g. while traveling).</summary>
    /// <example>permanent</example>
    public string PatientType { get; set; } = "permanent";

    /// <summary>Physician who referred the patient (sent as a second care team member).</summary>
    /// <example>prac-4</example>
    public string? ReferringPractitionerId { get; set; }

    /// <summary>Extra DTR forms to attach. Completed forms saved for these orders are attached automatically.</summary>
    /// <example>[]</example>
    public List<string> QuestionnaireResponseIds { get; set; } = new();

    /// <summary>EHR documents (DocumentReference ids) to attach now; the file content is embedded.</summary>
    /// <example>["doc-7001"]</example>
    public List<string> DocumentIds { get; set; } = new();

    /// <summary>Free-text note for the reviewer (max 264 characters are sent).</summary>
    /// <example>Patient started hemodialysis on 2026-09-01.</example>
    public string? Notes { get; set; }

    /// <summary>true (default) = also PUT the decision to the EHR.</summary>
    /// <example>true</example>
    public bool WriteBack { get; set; } = true;
}
