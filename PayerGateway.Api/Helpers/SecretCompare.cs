using System.Security.Cryptography;
using System.Text;

namespace PayerGateway.Api.Helpers;

/// <summary>Compares secrets in constant time (API keys, payer notification secrets).</summary>
public static class SecretCompare
{
    /// <summary>True when both strings are equal.</summary>
    public static bool AreEqual(string? a, string? b)
    {
        byte[] left = Encoding.UTF8.GetBytes(a ?? string.Empty);
        byte[] right = Encoding.UTF8.GetBytes(b ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    /// <summary>
    /// Checks the "Authorization: Bearer {secret}" header a payer sends on callbacks.
    /// An empty configured secret means "not checked" (local testing).
    /// </summary>
    public static bool BearerMatches(string? configuredSecret, string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(configuredSecret))
        {
            return true;
        }
        return AreEqual("Bearer " + configuredSecret, authorizationHeader);
    }
}
