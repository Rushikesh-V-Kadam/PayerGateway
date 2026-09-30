namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>Response of GET {payer}/cds-services: the CDS services (one per hook) the payer offers.</summary>
public class CdsDiscoveryResponse
{
    public List<CdsServiceDefinition> Services { get; set; } = new();
}
