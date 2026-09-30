using System.Collections.Concurrent;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Subscriptions in memory (local testing).</summary>
public class InMemoryPayerSubscriptionRepository : IPayerSubscriptionRepository
{
    private readonly ConcurrentDictionary<string, PayerSubscriptionRecord> _subscriptions = new ConcurrentDictionary<string, PayerSubscriptionRecord>();

    public Task<PayerSubscriptionRecord?> GetAsync(string payerName, string organizationNpi, CancellationToken ct)
    {
        PayerSubscriptionRecord? record;
        _subscriptions.TryGetValue(payerName.ToLowerInvariant() + "|" + organizationNpi, out record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(PayerSubscriptionRecord record, CancellationToken ct)
    {
        _subscriptions[record.PayerName.ToLowerInvariant() + "|" + record.OrganizationNpi] = record;
        return Task.CompletedTask;
    }
}
