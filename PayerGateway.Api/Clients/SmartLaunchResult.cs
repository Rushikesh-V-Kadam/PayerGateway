namespace PayerGateway.Api.Clients;

/// <summary>A SMART launch created at the Provider FHIR API: the launch id and the FHIR base URL (iss).</summary>
public class SmartLaunchResult
{
    public string LaunchId { get; set; } = string.Empty;
    public string Iss { get; set; } = string.Empty;
}
