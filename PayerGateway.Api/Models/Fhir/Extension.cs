using System.Text.Json.Serialization;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR Extension. Only one value[x] is set; a complex extension has child extensions instead.
/// Used to read CRD coverage-information, PAS review actions and CDex line numbers, and to write PAS / DTR extensions.
/// </summary>
public class Extension
{
    public string Url { get; set; } = string.Empty;

    /// <summary>Child extensions. Named Children because a member cannot share the class name; written as "extension".</summary>
    [JsonPropertyName("extension")]
    public List<Extension> Children { get; set; } = new();

    public string? ValueString { get; set; }
    public string? ValueCode { get; set; }
    public bool? ValueBoolean { get; set; }
    public int? ValueInteger { get; set; }
    public int? ValuePositiveInt { get; set; }
    public string? ValueDate { get; set; }
    public string? ValueUrl { get; set; }
    public string? ValueCanonical { get; set; }
    public CodeableConcept? ValueCodeableConcept { get; set; }
    public ResourceReference? ValueReference { get; set; }
    public Identifier? ValueIdentifier { get; set; }
    public Period? ValuePeriod { get; set; }
    public Quantity? ValueQuantity { get; set; }

    /// <summary>Empty extension (used by the JSON serializer).</summary>
    public Extension()
    {
    }

    /// <summary>Creates an extension with just its URL.</summary>
    public Extension(string url)
    {
        Url = url;
    }

    /// <summary>First child extension with this URL, or null.</summary>
    public Extension? Child(string url)
    {
        return Find(Children, url);
    }

    /// <summary>First extension with this URL in a list, or null.</summary>
    public static Extension? Find(List<Extension> extensions, string url)
    {
        foreach (Extension extension in extensions)
        {
            if (extension.Url == url)
            {
                return extension;
            }
        }
        return null;
    }
}
