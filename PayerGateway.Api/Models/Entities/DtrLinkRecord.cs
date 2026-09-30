namespace PayerGateway.Api.Models.Entities;

/// <summary>
/// A payer SMART DTR app link given to the EHR (table PA_DTR_LINK). Opening /api/v1/dtr/launch/{LinkId} uses these
/// values to create the SMART launch and open the payer's DTR app. Used when payers bring their own SMART apps.
/// </summary>
public class DtrLinkRecord
{
    public string LinkId { get; set; } = Guid.NewGuid().ToString("N");
    public string PayerName { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string? EncounterId { get; set; }
    public string? UserPractitionerId { get; set; }
    /// <summary>References the DTR app works on, e.g. "ServiceRequest/ord-1001", "Coverage/cov-1001".</summary>
    public List<string> FhirContext { get; set; } = new();
    /// <summary>appContext from the payer's card.</summary>
    public string? AppContext { get; set; }
    /// <summary>DTR app launch URL.</summary>
    public string AppUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
