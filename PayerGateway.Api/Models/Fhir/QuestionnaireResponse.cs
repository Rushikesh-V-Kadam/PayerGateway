using System.Text.Json.Nodes;

namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR QuestionnaireResponse (DTR profile): the filled-in payer form.
/// Built by the gateway from the EHR's plain answers, saved at the Provider FHIR API, and attached to PAS / CDex.
/// </summary>
public class QuestionnaireResponse
{
    public string ResourceType { get; set; } = "QuestionnaireResponse";
    public string? Id { get; set; }
    public Meta? Meta { get; set; }
    /// <summary>Adaptive forms: the payer Questionnaire (questions asked so far) travels inside the response.</summary>
    public List<JsonObject> Contained { get; set; } = new();
    /// <summary>qr-context extensions: which order / coverage the form is for.</summary>
    public List<Extension> Extension { get; set; } = new();
    /// <summary>Canonical URL (and version) of the payer Questionnaire.</summary>
    public string? Questionnaire { get; set; }
    /// <summary>in-progress | completed | amended</summary>
    public string? Status { get; set; }
    public ResourceReference? Subject { get; set; }
    public string? Authored { get; set; }
    public ResourceReference? Author { get; set; }
    public List<QuestionnaireResponseItem> Item { get; set; } = new();
}
