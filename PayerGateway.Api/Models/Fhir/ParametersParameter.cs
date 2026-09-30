using System.Text.Json.Nodes;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>One parameter of a Parameters resource: a value, a resource, or child parts.</summary>
public class ParametersParameter
{
    public string Name { get; set; } = string.Empty;
    public string? ValueString { get; set; }
    public string? ValueCode { get; set; }
    public bool? ValueBoolean { get; set; }
    public string? ValueCanonical { get; set; }
    public Identifier? ValueIdentifier { get; set; }
    public CodeableConcept? ValueCodeableConcept { get; set; }

    /// <summary>A whole resource as raw JSON.</summary>
    public JsonObject? Resource { get; set; }

    public List<ParametersParameter> Part { get; set; } = new();
}
