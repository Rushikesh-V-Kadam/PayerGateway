using System.Collections.Concurrent;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Tasks in memory (local testing).</summary>
public class InMemoryCdexTaskRepository : ICdexTaskRepository
{
    private readonly ConcurrentDictionary<string, CdexTaskRecord> _tasks = new ConcurrentDictionary<string, CdexTaskRecord>();

    public Task SaveAsync(CdexTaskRecord record, CancellationToken ct)
    {
        _tasks[record.PayerName + "|" + record.TaskId] = record;
        return Task.CompletedTask;
    }

    public Task<List<CdexTaskRecord>> GetForRequestAsync(string requestId, CancellationToken ct)
    {
        List<CdexTaskRecord> result = new List<CdexTaskRecord>();
        foreach (CdexTaskRecord record in _tasks.Values)
        {
            if (record.RequestId == requestId)
            {
                result.Add(record);
            }
        }
        return Task.FromResult(result);
    }
}
