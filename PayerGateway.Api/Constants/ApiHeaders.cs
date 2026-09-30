namespace PayerGateway.Api.Constants;

/// <summary>HTTP header names and content types.</summary>
public static class ApiHeaders
{
    /// <summary>EHR -> gateway API key (Security:EhrApiKey).</summary>
    public const string EhrApiKey = "X-Api-Key";

    /// <summary>Gateway -> Provider FHIR API shared secret.</summary>
    public const string InternalKey = "X-Internal-Key";

    /// <summary>Content type of FHIR JSON.</summary>
    public const string FhirJson = "application/fhir+json";

    /// <summary>Content type of plain JSON.</summary>
    public const string Json = "application/json";
}
