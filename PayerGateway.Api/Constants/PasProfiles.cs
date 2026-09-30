namespace PayerGateway.Api.Constants;

/// <summary>PAS 2.0.1 profile URLs.</summary>
public static class PasProfiles
{
    private const string Base = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/";
    public const string RequestBundle = Base + "profile-pas-request-bundle";
    public const string InquiryRequestBundle = Base + "profile-pas-inquiry-request-bundle";
    public const string Claim = Base + "profile-claim";
    public const string ClaimUpdate = Base + "profile-claim-update";
    public const string ClaimInquiry = Base + "profile-claim-inquiry";
    public const string Subscription = Base + "profile-subscription";
}
