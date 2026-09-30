namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Parameters: input / output of FHIR operations ($questionnaire-package, $submit-attachment).</summary>
public class Parameters
{
    public string ResourceType { get; set; } = "Parameters";
    public Meta? Meta { get; set; }
    public List<ParametersParameter> Parameter { get; set; } = new();
}
