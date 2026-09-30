using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Storage for PAS Subscriptions created at payers.</summary>
public interface IPayerSubscriptionRepository
{
    /// <summary>The subscription for a payer + organization NPI, or null.</summary>
    Task<PayerSubscriptionRecord?> GetAsync(string payerName, string organizationNpi, CancellationToken ct);

    /// <summary>Inserts or updates the subscription.</summary>
    Task SaveAsync(PayerSubscriptionRecord record, CancellationToken ct);
}
