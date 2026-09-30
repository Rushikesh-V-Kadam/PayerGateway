using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Storage for DTR forms (payer questionnaires fetched for an order, and their latest answers).</summary>
public interface IDtrFormRepository
{
    /// <summary>Inserts or updates the form.</summary>
    Task SaveAsync(DtrFormRecord record, CancellationToken ct);

    /// <summary>The form, or null when unknown.</summary>
    Task<DtrFormRecord?> GetAsync(string formId, CancellationToken ct);
}
