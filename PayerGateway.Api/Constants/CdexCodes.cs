namespace PayerGateway.Api.Constants;

/// <summary>CDex 2.0.0 $submit-attachment parameter names and codes.</summary>
public static class CdexCodes
{
    public const string SubmitAttachment = "$submit-attachment";
    public const string ParametersProfile = "http://hl7.org/fhir/us/davinci-cdex/StructureDefinition/cdex-parameters-submit-attachment";

    public const string TrackingId = "TrackingId";
    public const string AttachTo = "AttachTo";
    public const string PayerId = "PayerId";
    public const string OrganizationId = "OrganizationId";
    public const string ProviderId = "ProviderId";
    public const string MemberId = "MemberId";
    public const string Attachment = "Attachment";
    public const string LineItem = "LineItem";
    public const string Code = "Code";
    public const string Content = "Content";
    public const string Final = "Final";

    /// <summary>AttachTo value for prior authorization attachments.</summary>
    public const string PreAuthorization = "preauthorization";

    /// <summary>Task.identifier type code of the payer tracking id.</summary>
    public const string TrackingIdType = "tracking-id";

    /// <summary>LOINC "payer questionnaire" code for a QuestionnaireResponse attachment.</summary>
    public const string PayerQuestionnaireLoinc = "102089-0";
}
