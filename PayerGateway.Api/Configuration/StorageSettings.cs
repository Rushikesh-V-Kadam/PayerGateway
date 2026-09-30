namespace PayerGateway.Api.Configuration;

/// <summary>Where gateway data is stored (appsettings "Storage").</summary>
public class StorageSettings
{
    /// <summary>Name of the appsettings section.</summary>
    public const string SectionName = "Storage";

    /// <summary>"Oracle" (uses ConnectionStrings:Oracle) or "InMemory" (local testing, lost on restart).</summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>True when Oracle is selected.</summary>
    public bool IsOracle()
    {
        return string.Equals(Provider, "Oracle", StringComparison.OrdinalIgnoreCase);
    }
}
