using System.Collections.Concurrent;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>DTR links in memory (local testing).</summary>
public class InMemoryDtrLinkRepository : IDtrLinkRepository
{
    private readonly ConcurrentDictionary<string, DtrLinkRecord> _links = new ConcurrentDictionary<string, DtrLinkRecord>();

    public Task SaveAsync(DtrLinkRecord record, CancellationToken ct)
    {
        _links[record.LinkId] = record;
        return Task.CompletedTask;
    }

    public Task<DtrLinkRecord?> GetAsync(string linkId, CancellationToken ct)
    {
        DtrLinkRecord? record;
        _links.TryGetValue(linkId, out record);
        return Task.FromResult(record);
    }
}
