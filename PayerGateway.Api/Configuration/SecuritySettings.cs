namespace PayerGateway.Api.Configuration;

/// <summary>Gateway security (appsettings "Security").</summary>
public class SecuritySettings
{
    /// <summary>Name of the appsettings section.</summary>
    public const string SectionName = "Security";

    /// <summary>Public URL of the gateway; used as "iss" in the JWTs sent to payers.</summary>
    public string Issuer { get; set; } = "http://localhost:5200";

    /// <summary>PEM file with the RSA private key for JWT signing. Empty = a new key at every start (development only).</summary>
    public string? SigningKeyPemPath { get; set; }

    /// <summary>When set, the EHR must send it in the X-Api-Key header on /api/v1 calls.</summary>
    public string? EhrApiKey { get; set; }
}
