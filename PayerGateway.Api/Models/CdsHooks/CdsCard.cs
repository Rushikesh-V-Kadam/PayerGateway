namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>A CDS Hooks card: a message for the clinician, optionally with links (e.g. a SMART link to the DTR app).</summary>
public class CdsCard
{
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    /// <summary>info | warning | critical</summary>
    public string Indicator { get; set; } = "info";
    public List<CdsLink> Links { get; set; } = new();
}
