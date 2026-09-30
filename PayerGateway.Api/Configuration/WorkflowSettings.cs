namespace PayerGateway.Api.Configuration;

/// <summary>Workflow options (appsettings "Workflow").</summary>
public class WorkflowSettings
{
    /// <summary>Name of the appsettings section.</summary>
    public const string SectionName = "Workflow";

    /// <summary>Public URL of the gateway; used in DTR links and in the PAS Subscription endpoint.</summary>
    public string GatewayPublicBaseUrl { get; set; } = "http://localhost:5200";

    /// <summary>Identifier system of our prior auth request ids (Claim.identifier).</summary>
    public string ClaimIdentifierSystem { get; set; } = "http://provider.example.org/fhir/sid/prior-auth-request-id";

    /// <summary>Identifier system of PAS Bundle ids.</summary>
    public string BundleIdentifierSystem { get; set; } = "http://provider.example.org/fhir/sid/pas-bundle-id";

    /// <summary>SMART DTR app launch URL (future, payer apps). Empty = use the URL from the payer's card.</summary>
    public string? DtrAppUrl { get; set; }

    /// <summary>How long a DTR link written to the EHR stays valid, in hours.</summary>
    public int DtrLinkHours { get; set; } = 72;

    /// <summary>Lifetime of the fhirAuthorization token given to payers during CRD, in minutes.</summary>
    public int FhirAuthorizationMinutes { get; set; } = 5;

    /// <summary>How often the background job checks pended requests ($inquire + CDex Tasks), in minutes. 0 = off.</summary>
    public int PollMinutes { get; set; } = 5;

    /// <summary>Decision deadline for urgent requests (CMS-0057-F: 72 hours).</summary>
    public int ExpeditedDecisionHours { get; set; } = 72;

    /// <summary>Decision deadline for routine requests (CMS-0057-F: 7 days).</summary>
    public int StandardDecisionDays { get; set; } = 7;

    /// <summary>GatewayPublicBaseUrl without a trailing slash.</summary>
    public string GetGatewayBaseUrl()
    {
        return GatewayPublicBaseUrl.TrimEnd('/');
    }
}
