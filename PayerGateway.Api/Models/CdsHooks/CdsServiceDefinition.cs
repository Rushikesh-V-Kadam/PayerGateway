namespace PayerGateway.Api.Models.CdsHooks;

/// <summary>One CDS service: which hook it handles, its id (used in the URL) and its prefetch templates.</summary>
public class CdsServiceDefinition
{
    public string Hook { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>key -> FHIR query template, e.g. "coverage": "Coverage?patient={{context.patientId}}".</summary>
    public Dictionary<string, string>? Prefetch { get; set; }
}
