namespace PayerGateway.Api.Constants;

/// <summary>CRD 2.0.1: the order-sign hook, the coverage-information extension URL, its parts, and code values.</summary>
public static class CrdCodes
{
    /// <summary>The only CDS hook this gateway uses: the order is being signed.</summary>
    public const string OrderSignHook = "order-sign";

    public const string CoverageInformationUrl = "http://hl7.org/fhir/us/davinci-crd/StructureDefinition/ext-coverage-information";

    // parts of ext-coverage-information
    public const string PartCoverage = "coverage";
    public const string PartCovered = "covered";
    public const string PartPaNeeded = "pa-needed";
    public const string PartDocNeeded = "doc-needed";
    public const string PartDocPurpose = "doc-purpose";
    public const string PartInfoNeeded = "info-needed";
    public const string PartReason = "reason";
    public const string PartQuestionnaire = "questionnaire";
    public const string PartDate = "date";
    public const string PartCoverageAssertionId = "coverage-assertion-id";
    public const string PartSatisfiedPaId = "satisfied-pa-id";

    // values
    public const string NotCovered = "not-covered";
    public const string Conditional = "conditional";
    public const string Unknown = "unknown";
    public const string AuthNeeded = "auth-needed";
    public const string PerformPa = "performpa";
    public const string NoDoc = "no-doc";
    public const string LinkTypeSmart = "smart";
    public const string LinkTypeAbsolute = "absolute";
}
