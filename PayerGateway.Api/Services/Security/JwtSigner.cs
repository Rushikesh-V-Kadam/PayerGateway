using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;

namespace PayerGateway.Api.Services.Security;

/// <summary>
/// Signs JWTs (RS384) with the gateway's RSA key:
///  - the CDS Hooks JWT sent with every CRD hook call (CDS Hooks 2.0 security),
///  - the client assertion for "smart-backend" payer auth (SMART Backend Services, private_key_jwt).
/// Payers download the public key from GET /.well-known/jwks.json. Registered as a singleton.
/// </summary>
public sealed class JwtSigner : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _issuer;

    /// <summary>Key id ("kid") published in the JWKS.</summary>
    public string KeyId { get; }

    /// <summary>Loads the key from Security:SigningKeyPemPath, or keeps a new random key (development only).</summary>
    public JwtSigner(IOptions<SecuritySettings> settings)
    {
        _issuer = settings.Value.Issuer;
        string? path = settings.Value.SigningKeyPemPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            _rsa.ImportFromPem(File.ReadAllText(path));
        }
        byte[] modulus = _rsa.ExportParameters(false).Modulus ?? new byte[0];
        KeyId = Base64Url(SHA256.HashData(modulus)).Substring(0, 16);
    }

    /// <summary>JWT for one CDS Hooks call: iss = gateway, aud = the CDS service URL, valid 5 minutes.</summary>
    public string CreateCdsHooksJwt(string serviceUrl)
    {
        Dictionary<string, object> claims = new Dictionary<string, object>();
        claims["iss"] = _issuer;
        claims["sub"] = _issuer;
        claims["aud"] = serviceUrl;
        claims["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        claims["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        claims["jti"] = Guid.NewGuid().ToString("N");
        return Sign(claims);
    }

    /// <summary>Client assertion for SMART Backend Services: iss = sub = client id, aud = token URL.</summary>
    public string CreateClientAssertion(string clientId, string tokenUrl)
    {
        Dictionary<string, object> claims = new Dictionary<string, object>();
        claims["iss"] = clientId;
        claims["sub"] = clientId;
        claims["aud"] = tokenUrl;
        claims["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        claims["jti"] = Guid.NewGuid().ToString("N");
        return Sign(claims);
    }

    /// <summary>The public key as a JSON Web Key Set.</summary>
    public Dictionary<string, object> GetJwks()
    {
        RSAParameters key = _rsa.ExportParameters(false);
        Dictionary<string, string> jwk = new Dictionary<string, string>();
        jwk["kty"] = "RSA";
        jwk["use"] = "sig";
        jwk["alg"] = "RS384";
        jwk["kid"] = KeyId;
        jwk["n"] = Base64Url(key.Modulus ?? new byte[0]);
        jwk["e"] = Base64Url(key.Exponent ?? new byte[0]);

        List<Dictionary<string, string>> keys = new List<Dictionary<string, string>>();
        keys.Add(jwk);
        Dictionary<string, object> jwks = new Dictionary<string, object>();
        jwks["keys"] = keys;
        return jwks;
    }

    /// <summary>header.payload.signature</summary>
    private string Sign(Dictionary<string, object> claims)
    {
        Dictionary<string, string> headerValues = new Dictionary<string, string>();
        headerValues["alg"] = "RS384";
        headerValues["typ"] = "JWT";
        headerValues["kid"] = KeyId;

        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(headerValues));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        byte[] signature = _rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);
        return header + "." + payload + "." + Base64Url(signature);
    }

    /// <summary>Base64url without padding.</summary>
    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Releases the key.</summary>
    public void Dispose()
    {
        _rsa.Dispose();
    }
}
