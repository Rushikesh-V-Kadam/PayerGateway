using System.Text.Json.Nodes;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Models.Fhir;

namespace PayerGateway.Api.Services.Pas;

/// <summary>Everything PasBundleBuilder needs for one PAS request Bundle (all read by PasService beforehand).</summary>
public class PasBuildInput
{
    public string RequestId { get; set; } = string.Empty;
    /// <summary>1 = first submission; higher = update / cancel (uses the Claim Update profile).</summary>
    public int Version { get; set; } = 1;
    /// <summary>routine | urgent</summary>
    public string Priority { get; set; } = "routine";
    public PayerSettings Payer { get; set; } = new PayerSettings();

    public JsonObject Patient { get; set; } = new JsonObject();
    public Coverage Coverage { get; set; } = new Coverage();
    /// <summary>Requesting provider organization (Claim.provider).</summary>
    public JsonObject Organization { get; set; } = new JsonObject();
    public JsonObject? Practitioner { get; set; }

    /// <summary>Orders requested now (active lines).</summary>
    public List<JsonObject> Orders { get; set; } = new List<JsonObject>();
    /// <summary>Orders removed from the request (lines sent as cancelled).</summary>
    public List<JsonObject> CancelledOrders { get; set; } = new List<JsonObject>();
    /// <summary>Order ids of the previous version (new orders are marked "added").</summary>
    public List<string> PreviousOrderIds { get; set; } = new List<string>();
    /// <summary>orderId -> claim line number.</summary>
    public Dictionary<string, int> LineNumbers { get; set; } = new Dictionary<string, int>();

    /// <summary>DTR forms (raw QuestionnaireResponse JSON).</summary>
    public List<JsonObject> Forms { get; set; } = new List<JsonObject>();
    /// <summary>Documents with their file embedded (raw DocumentReference JSON).</summary>
    public List<JsonObject> Documents { get; set; } = new List<JsonObject>();
    public string? Notes { get; set; }

    /// <summary>new | renewal</summary>
    public string RequestType { get; set; } = "new";
    public string? PreviousAuthorizationNumber { get; set; }
    public string? PreviousServiceStart { get; set; }
    public string? PreviousServiceEnd { get; set; }
    public int? PreviousTreatments { get; set; }
    /// <summary>permanent | transient</summary>
    public string PatientType { get; set; } = "permanent";
    /// <summary>Referring physician (Claim.careTeam sequence 2), read from the Provider FHIR API.</summary>
    public JsonObject? ReferringPractitioner { get; set; }
}
