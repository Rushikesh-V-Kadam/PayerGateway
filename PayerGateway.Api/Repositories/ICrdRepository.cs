using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Storage for CRD: order-sign answers (so a repeated eventId is not sent twice) and the latest decision per order.</summary>
public interface ICrdRepository
{
    /// <summary>Stored answer of an event, or null when the eventId is new.</summary>
    Task<CrdEventRecord?> GetEventAsync(string eventId, CancellationToken ct);

    /// <summary>Stores the answer of an event.</summary>
    Task SaveEventAsync(CrdEventRecord record, CancellationToken ct);

    /// <summary>Latest decision of an order, or null.</summary>
    Task<CoverageDecisionRecord?> GetDecisionAsync(string orderId, CancellationToken ct);

    /// <summary>Inserts or replaces the order's decision.</summary>
    Task SaveDecisionAsync(CoverageDecisionRecord record, CancellationToken ct);
}
