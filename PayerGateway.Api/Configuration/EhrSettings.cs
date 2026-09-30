namespace PayerGateway.Api.Configuration;

/// <summary>How to reach the EHR for write-backs (appsettings "Ehr").</summary>
public class EhrSettings
{
    /// <summary>Name of the appsettings section.</summary>
    public const string SectionName = "Ehr";

    /// <summary>EHR REST base URL (mock EHR: http://localhost:5090).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5090";

    /// <summary>Sent as X-Api-Key to the EHR when set.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Timeout of each write-back, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
