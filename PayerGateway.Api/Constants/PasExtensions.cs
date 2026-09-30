namespace PayerGateway.Api.Constants;

/// <summary>PAS 2.0.1 extension URLs used on Claim.item and ClaimResponse.item.</summary>
public static class PasExtensions
{
    private const string Base = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/";

    // Claim.item (request)
    public const string CertificationType = Base + "extension-certificationType";
    public const string ServiceItemRequestType = Base + "extension-serviceItemRequestType";
    public const string RequestedService = Base + "extension-requestedService";
    public const string InfoChanged = Base + "extension-infoChanged";
    public const string InfoCancelled = Base + "modifierextension-infoCancelled";

    // ClaimResponse.item (decision)
    public const string ReviewAction = Base + "extension-reviewAction";
    public const string ReviewActionCode = Base + "extension-reviewActionCode";
    public const string ItemPreAuthPeriod = Base + "extension-itemPreAuthPeriod";
    public const string ItemAuthorizedDetail = Base + "extension-itemAuthorizedDetail";

    // Task.input line numbers (CDex)
    public const string PaLineNumber = Base + "extension-paLineNumber";
    public const string ServiceLineNumber = Base + "extension-serviceLineNumber";
}
