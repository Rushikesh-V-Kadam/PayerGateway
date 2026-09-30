using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Storage for payer SMART DTR app links written to the EHR.</summary>
public interface IDtrLinkRepository
{
    /// <summary>Stores a new link.</summary>
    Task SaveAsync(DtrLinkRecord record, CancellationToken ct);

    /// <summary>The link, or null when unknown.</summary>
    Task<DtrLinkRecord?> GetAsync(string linkId, CancellationToken ct);
}
