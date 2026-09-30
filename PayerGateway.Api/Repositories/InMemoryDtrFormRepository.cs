using System.Collections.Concurrent;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>DTR forms in memory (local testing).</summary>
public class InMemoryDtrFormRepository : IDtrFormRepository
{
    private readonly ConcurrentDictionary<string, DtrFormRecord> _forms = new ConcurrentDictionary<string, DtrFormRecord>();

    public Task SaveAsync(DtrFormRecord record, CancellationToken ct)
    {
        record.UpdatedAt = DateTime.UtcNow;
        _forms[record.FormId] = record;
        return Task.CompletedTask;
    }

    public Task<DtrFormRecord?> GetAsync(string formId, CancellationToken ct)
    {
        DtrFormRecord? record;
        _forms.TryGetValue(formId, out record);
        return Task.FromResult(record);
    }
}
