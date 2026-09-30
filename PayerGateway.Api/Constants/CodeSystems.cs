namespace PayerGateway.Api.Constants;

/// <summary>Code system and identifier system URIs.</summary>
public static class CodeSystems
{
    public const string Npi = "http://hl7.org/fhir/sid/us-npi";
    public const string Loinc = "http://loinc.org";
    public const string ClaimType = "http://terminology.hl7.org/CodeSystem/claim-type";
    public const string ProcessPriority = "http://terminology.hl7.org/CodeSystem/processpriority";
    public const string CareTeamRole = "http://terminology.hl7.org/CodeSystem/claimcareteamrole";
    public const string RelatedClaimRelationship = "http://terminology.hl7.org/CodeSystem/ex-relatedclaimrelationship";
    public const string PasTemp = "http://hl7.org/fhir/us/davinci-pas/CodeSystem/PASTempCodes";
    public const string PlaceOfService = "https://www.cms.gov/Medicare/Coding/place-of-service-codes/Place_of_Service_Code_Set";
    public const string X12ServiceType = "https://codesystem.x12.org/005010/1365";
    public const string X12CertificationType = "https://codesystem.x12.org/005010/1322";
    public const string X12RequestType = "https://codesystem.x12.org/005010/1525";
}
