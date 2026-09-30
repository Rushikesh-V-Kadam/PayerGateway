namespace PayerGateway.Api.Models.Responses;

/// <summary>
/// A link on a message. type "smart" = a payer DTR app: a plain gateway URL (/api/v1/dtr/launch/{linkId}) the EHR
/// opens in a browser; the gateway then starts the SMART app. type "absolute" = a normal web page.
/// </summary>
public class EhrMessageLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>absolute | smart</summary>
    public string Type { get; set; } = "absolute";
}
