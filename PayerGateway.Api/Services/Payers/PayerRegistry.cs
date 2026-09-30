using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Models.CdsHooks;

namespace PayerGateway.Api.Services.Payers;

/// <summary>
/// The list of connected payers (appsettings "Payers"). The EHR sends payerName in each request and the gateway
/// looks the payer up here by that name (upper / lower case does not matter).
/// Also caches each payer's CDS Hooks discovery (services + prefetch templates) for 12 hours.
/// Registered as a singleton.
/// </summary>
public class PayerRegistry
{
    private readonly List<PayerSettings> _payers;
    private readonly ConcurrentDictionary<string, CachedDiscovery> _discovery = new ConcurrentDictionary<string, CachedDiscovery>();

    /// <summary>Loads the payer list from appsettings.</summary>
    public PayerRegistry(IOptions<List<PayerSettings>> payers)
    {
        _payers = payers.Value;
    }

    /// <summary>All configured payers.</summary>
    public List<PayerSettings> GetAll()
    {
        return _payers;
    }

    /// <summary>The enabled payer with this name, or null.</summary>
    public PayerSettings? Find(string? payerName)
    {
        if (string.IsNullOrWhiteSpace(payerName))
        {
            return null;
        }
        foreach (PayerSettings payer in _payers)
        {
            if (payer.Enabled && string.Equals(payer.PayerName, payerName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return payer;
            }
        }
        return null;
    }

    /// <summary>
    /// The enabled payer with this name. Throws ApiException with <paramref name="statusCode"/> when it is not
    /// configured (400 when the name came in a request body, 404 when it came in the URL).
    /// </summary>
    public PayerSettings GetRequired(string? payerName, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(payerName))
        {
            throw new ApiException(400, "payerName is required.");
        }
        PayerSettings? payer = Find(payerName);
        if (payer == null)
        {
            throw new ApiException(statusCode, "Payer '" + payerName + "' is not configured or not enabled. Configured payers: " + ConfiguredNames() + ".");
        }
        return payer;
    }

    /// <summary>Cached discovery of a payer, or null when not cached / expired.</summary>
    public CdsDiscoveryResponse? GetCachedDiscovery(string payerName)
    {
        CachedDiscovery? cached;
        if (_discovery.TryGetValue(payerName.ToLowerInvariant(), out cached) && cached.Expires > DateTime.UtcNow)
        {
            return cached.Discovery;
        }
        return null;
    }

    /// <summary>Stores a payer's discovery for 12 hours.</summary>
    public void CacheDiscovery(string payerName, CdsDiscoveryResponse discovery)
    {
        CachedDiscovery cached = new CachedDiscovery();
        cached.Discovery = discovery;
        cached.Expires = DateTime.UtcNow.AddHours(12);
        _discovery[payerName.ToLowerInvariant()] = cached;
    }

    /// <summary>"A, B, C" list of enabled payer names, for error messages.</summary>
    private string ConfiguredNames()
    {
        List<string> names = new List<string>();
        foreach (PayerSettings payer in _payers)
        {
            if (payer.Enabled)
            {
                names.Add(payer.PayerName);
            }
        }
        if (names.Count == 0)
        {
            return "(none)";
        }
        return string.Join(", ", names);
    }
}
