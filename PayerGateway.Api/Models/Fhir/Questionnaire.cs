namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR Questionnaire (DTR profile) as returned by the payer's $questionnaire-package.
/// Only the parts the gateway needs to show the questions and build the QuestionnaireResponse.
/// </summary>
public class Questionnaire
{
    public string ResourceType { get; set; } = "Questionnaire";
    public string? Id { get; set; }
    /// <summary>Canonical URL, e.g. http://payer.example.org/fhir/Questionnaire/dialysis-pa.</summary>
    public string? Url { get; set; }
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Status { get; set; }
    /// <summary>Holds sdc-questionnaire-adaptive when the payer sends the questions one at a time ($next-question).</summary>
    public List<Extension> Extension { get; set; } = new();
    public List<QuestionnaireItem> Item { get; set; } = new();

    /// <summary>"url|version" when a version is known, else the url.</summary>
    public string? GetCanonical()
    {
        if (string.IsNullOrEmpty(Url))
        {
            return null;
        }
        if (string.IsNullOrEmpty(Version))
        {
            return Url;
        }
        return Url + "|" + Version;
    }
}
