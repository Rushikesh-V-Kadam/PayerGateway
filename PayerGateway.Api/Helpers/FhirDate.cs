namespace PayerGateway.Api.Helpers;

/// <summary>FHIR date / dateTime strings.</summary>
public static class FhirDate
{
    /// <summary>Now as a FHIR instant, e.g. "2026-10-01T10:00:00Z".</summary>
    public static string Now()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
    }

    /// <summary>A UTC time as a FHIR instant.</summary>
    public static string Instant(DateTime utc)
    {
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
    }

    /// <summary>Today as a FHIR date.</summary>
    public static string Today()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }
}
