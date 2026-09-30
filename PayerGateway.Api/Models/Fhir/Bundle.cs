using System.Text.Json.Nodes;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR Bundle. Entries hold the resource as raw JSON so any resource type can travel through the gateway
/// unchanged (read a typed copy with FhirJson.Read when a field is needed).
/// </summary>
public class Bundle
{
    public string ResourceType { get; set; } = "Bundle";
    public string? Id { get; set; }
    public Meta? Meta { get; set; }
    public Identifier? Identifier { get; set; }
    /// <summary>collection | searchset | history ...</summary>
    public string? Type { get; set; }
    public string? Timestamp { get; set; }
    public int? Total { get; set; }
    public List<BundleEntry> Entry { get; set; } = new();

    /// <summary>All entry resources of one type, as raw JSON.</summary>
    public List<JsonObject> ResourcesOfType(string resourceType)
    {
        List<JsonObject> result = new List<JsonObject>();
        foreach (BundleEntry entry in Entry)
        {
            if (entry.Resource == null)
            {
                continue;
            }
            JsonValue? type = entry.Resource["resourceType"] as JsonValue;
            string? typeText;
            if (type != null && type.TryGetValue<string>(out typeText) && typeText == resourceType)
            {
                result.Add(entry.Resource);
            }
        }
        return result;
    }
}
