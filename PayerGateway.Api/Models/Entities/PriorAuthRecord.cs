using PayerGateway.Api.Constants;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Models.Entities;

/// <summary>One prior authorization request and its latest decision (table PA_PRIOR_AUTH).</summary>
public class PriorAuthRecord
{
    public string RequestId { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string CoverageId { get; set; } = string.Empty;
    public string? MemberId { get; set; }
    public string OrganizationId { get; set; } = string.Empty;
    public string? OrganizationNpi { get; set; }
    public string? PractitionerId { get; set; }
    public string? PractitionerNpi { get; set; }
    /// <summary>routine | urgent</summary>
    public string Priority { get; set; } = "routine";

    /// <summary>Overall status (copy of Decision.OverallStatus, kept as a column for queries).</summary>
    public string Status { get; set; } = DecisionStatus.Pended;
    public string? AuthorizationNumber { get; set; }

    /// <summary>1 = first submission, +1 for every update / cancel.</summary>
    public int Version { get; set; }

    /// <summary>Orders currently in the request.</summary>
    public List<string> OrderIds { get; set; } = new();

    /// <summary>orderId -> Claim.item.sequence. Kept so an order keeps its line number on updates.</summary>
    public Dictionary<string, int> LineNumbers { get; set; } = new();

    /// <summary>Latest decision (returned to and written to the EHR).</summary>
    public PriorAuthDecision Decision { get; set; } = new();

    /// <summary>Last PAS Bundle sent (needed to build $inquire).</summary>
    public string? LastRequestBundleJson { get; set; }

    /// <summary>Last payer response Bundle (for support).</summary>
    public string? LastResponseBundleJson { get; set; }

    /// <summary>SENT | FAILED - whether the last decision reached the EHR (FAILED is retried by the background job).</summary>
    public string EhrSyncStatus { get; set; } = PasCodes.EhrSyncSent;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True while the payer has not decided yet.</summary>
    public bool IsOpen()
    {
        return Status == DecisionStatus.Pended;
    }
}
