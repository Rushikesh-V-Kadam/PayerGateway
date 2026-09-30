namespace PayerGateway.Api.Configuration;

/// <summary>
/// One connected payer (appsettings "Payers" list). The EHR sends <see cref="PayerName"/> in every request;
/// the gateway finds the matching entry here and uses its URLs and credentials. To add a payer, add an entry.
/// Every payer is called the same way; only these values differ:
///   CRD   {BaseUrl}/{CdsServicesPath}            e.g. http://localhost:5080/cds-services
///   DTR   {BaseUrl}/{FhirPath}/Questionnaire/$questionnaire-package
///   PAS   {BaseUrl}/{FhirPath}/Claim/$submit, Claim/$inquire, Subscription
///   CDex  {BaseUrl}/{FhirPath}/$submit-attachment, Task
/// </summary>
public class PayerSettings
{
    /// <summary>Name of the appsettings section that holds the payer list.</summary>
    public const string SectionName = "Payers";

    /// <summary>The name the EHR sends (payerName), e.g. "MockPayer". Matched without regard to upper / lower case.</summary>
    public string PayerName { get; set; } = string.Empty;

    /// <summary>Friendly name for messages and logs, e.g. "Mock Da Vinci Payer".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>false = the payer is configured but switched off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The payer's own id, sent to the payer in PAS (insurer identifier) and CDex (PayerId), e.g. "PAYER001".</summary>
    public string PayerIdentifier { get; set; } = string.Empty;

    /// <summary>Payer base URL, e.g. https://api.payer.com.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>CDS Hooks path under BaseUrl (default "cds-services").</summary>
    public string CdsServicesPath { get; set; } = "cds-services";

    /// <summary>FHIR path under BaseUrl (default "fhir").</summary>
    public string FhirPath { get; set; } = "fhir";

    /// <summary>How the gateway authenticates to the payer's FHIR API.</summary>
    public PayerAuthSettings Auth { get; set; } = new PayerAuthSettings();

    /// <summary>Send a signed JWT with each CDS Hooks call (CDS Hooks 2.0 requires it in production).</summary>
    public bool SignCdsHooksRequests { get; set; } = true;

    /// <summary>Create a PAS Subscription so the payer notifies us when a pended request is decided.</summary>
    public bool UseSubscriptions { get; set; } = true;

    /// <summary>Secret the payer sends as "Authorization: Bearer ..." on notifications / pushed Tasks. Empty = not checked.</summary>
    public string? NotificationSecret { get; set; }

    /// <summary>Identifier system this payer expects for member ids.</summary>
    public string MemberIdSystem { get; set; } = "http://example.org/member-ids";

    /// <summary>Identifier system this payer expects for its payer id.</summary>
    public string PayerIdSystem { get; set; } = "http://example.org/payer-ids";

    /// <summary>CRD must be fast (the clinician is waiting): seconds before giving up.</summary>
    public int CrdTimeoutSeconds { get; set; } = 5;

    /// <summary>Timeout of every other payer call, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>{BaseUrl}/{CdsServicesPath}</summary>
    public string GetCdsServicesUrl()
    {
        return BaseUrl.TrimEnd('/') + "/" + CdsServicesPath.Trim('/');
    }

    /// <summary>{BaseUrl}/{FhirPath}</summary>
    public string GetFhirBaseUrl()
    {
        return BaseUrl.TrimEnd('/') + "/" + FhirPath.Trim('/');
    }

    /// <summary>DisplayName, or PayerName when no display name is set.</summary>
    public string GetDisplayName()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            return PayerName;
        }
        return DisplayName;
    }
}
