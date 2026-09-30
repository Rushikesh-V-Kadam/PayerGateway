namespace PayerGateway.Api.Configuration;

/// <summary>How to reach the Provider FHIR API (appsettings "FhirApi"). All EHR data is read there as FHIR.</summary>
public class FhirApiSettings
{
    /// <summary>Name of the appsettings section.</summary>
    public const string SectionName = "FhirApi";

    /// <summary>FHIR base URL, e.g. http://localhost:5100/fhir/r4. Also sent to payers as fhirServer.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5100/fhir/r4";

    /// <summary>Root URL for the /internal endpoints, e.g. http://localhost:5100.</summary>
    public string InternalBaseUrl { get; set; } = "http://localhost:5100";

    /// <summary>Must equal Security:InternalApiKey in the Provider FHIR API.</summary>
    public string InternalApiKey { get; set; } = string.Empty;

    /// <summary>Timeout of each call, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>BaseUrl without a trailing slash.</summary>
    public string GetBaseUrl()
    {
        return BaseUrl.TrimEnd('/');
    }
}
