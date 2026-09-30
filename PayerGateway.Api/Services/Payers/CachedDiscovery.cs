using PayerGateway.Api.Models.CdsHooks;

namespace PayerGateway.Api.Services.Payers;

/// <summary>A payer's CDS Hooks discovery and when the cached copy expires.</summary>
public class CachedDiscovery
{
    public CdsDiscoveryResponse Discovery { get; set; } = new CdsDiscoveryResponse();
    public DateTime Expires { get; set; }
}
