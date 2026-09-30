namespace PayerGateway.Api.Configuration;

/// <summary>
/// Payer authentication for FHIR calls (DTR, PAS, CDex):
///   "none"              no token (mock payer)
///   "client-secret"     OAuth client_credentials with client id + secret
///   "smart-backend"     SMART Backend Services: client_credentials with a signed JWT (private_key_jwt)
/// </summary>
public class PayerAuthSettings
{
    /// <summary>none | client-secret | smart-backend</summary>
    public string Type { get; set; } = "none";

    /// <summary>The payer's OAuth token URL.</summary>
    public string? TokenUrl { get; set; }

    /// <summary>Client id registered with the payer.</summary>
    public string? ClientId { get; set; }

    /// <summary>Client secret (client-secret only).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Scopes asked for.</summary>
    public string Scope { get; set; } = "system/Claim.c system/ClaimResponse.r system/Questionnaire.r system/Task.ru";
}
