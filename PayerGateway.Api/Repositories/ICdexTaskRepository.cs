using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Storage for payer information requests (Tasks).</summary>
public interface ICdexTaskRepository
{
    /// <summary>Inserts or updates the Task.</summary>
    Task SaveAsync(CdexTaskRecord record, CancellationToken ct);

    /// <summary>All Tasks of one prior auth request.</summary>
    Task<List<CdexTaskRecord>> GetForRequestAsync(string requestId, CancellationToken ct);
}
