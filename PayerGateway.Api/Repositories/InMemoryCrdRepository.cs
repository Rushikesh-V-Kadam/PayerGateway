using System.Collections.Concurrent;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>CRD storage in memory (local testing; lost on restart).</summary>
public class InMemoryCrdRepository : ICrdRepository
{
    private readonly ConcurrentDictionary<string, CrdEventRecord> _events = new ConcurrentDictionary<string, CrdEventRecord>();
    private readonly ConcurrentDictionary<string, CoverageDecisionRecord> _decisions = new ConcurrentDictionary<string, CoverageDecisionRecord>();

    public Task<CrdEventRecord?> GetEventAsync(string eventId, CancellationToken ct)
    {
        CrdEventRecord? record;
        _events.TryGetValue(eventId, out record);
        return Task.FromResult(record);
    }

    public Task SaveEventAsync(CrdEventRecord record, CancellationToken ct)
    {
        _events[record.EventId] = record;
        return Task.CompletedTask;
    }

    public Task<CoverageDecisionRecord?> GetDecisionAsync(string orderId, CancellationToken ct)
    {
        CoverageDecisionRecord? record;
        _decisions.TryGetValue(orderId, out record);
        return Task.FromResult(record);
    }

    public Task SaveDecisionAsync(CoverageDecisionRecord record, CancellationToken ct)
    {
        _decisions[record.OrderId] = record;
        return Task.CompletedTask;
    }
}
