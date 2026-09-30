namespace PayerGateway.Api.Services.Payers;

/// <summary>An OAuth access token of a payer and when it expires.</summary>
public class CachedToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
}
