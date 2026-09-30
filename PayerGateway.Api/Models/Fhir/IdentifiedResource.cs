namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// Minimal typed view of Organization / Practitioner / Patient: only the id and identifiers
/// (the gateway needs the NPI). The full resource travels as raw JSON.
/// </summary>
public class IdentifiedResource
{
    public string? ResourceType { get; set; }
    public string? Id { get; set; }
    public List<Identifier> Identifier { get; set; } = new();

    /// <summary>Value of the identifier with this system (e.g. the NPI), or null.</summary>
    public string? IdentifierValue(string system)
    {
        foreach (Identifier identifier in Identifier)
        {
            if (identifier.System == system)
            {
                return identifier.Value;
            }
        }
        return null;
    }
}
