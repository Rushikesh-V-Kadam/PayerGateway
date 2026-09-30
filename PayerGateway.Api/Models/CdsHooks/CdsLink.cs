namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>A link on a card. type = "smart" means "launch this SMART app" (DTR); "absolute" is a normal web link.</summary>
public class CdsLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = "absolute";
    /// <summary>For SMART links: context string handed to the app (questionnaires, coverage-assertion-id).</summary>
    public string? AppContext { get; set; }
}
