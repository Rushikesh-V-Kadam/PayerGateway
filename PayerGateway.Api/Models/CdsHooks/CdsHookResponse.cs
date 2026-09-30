namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>The payer's CDS Hooks answer: cards (messages for the user) and systemActions (coverage information).</summary>
public class CdsHookResponse
{
    public List<CdsCard> Cards { get; set; } = new();
    public List<CdsAction> SystemActions { get; set; } = new();
}
