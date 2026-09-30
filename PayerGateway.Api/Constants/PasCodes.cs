namespace PayerGateway.Api.Constants;

/// <summary>PAS operations and PAS temporary codes.</summary>
public static class PasCodes
{
    public const string Submit = "Claim/$submit";
    public const string Inquire = "Claim/$inquire";

    // PASTempCodes
    public const string AdditionalInformation = "additionalInformation";
    public const string FreeFormMessage = "freeFormMessage";
    public const string AttachmentsNeeded = "attachments-needed";
    public const string QuestionnairesNeeded = "questionnaires-needed";
    public const string PayerUrl = "payer-url";
    public const string Added = "added";

    // PAS Subscription (R5 Backport)
    public const string SubscriptionTopic = "http://hl7.org/fhir/us/davinci-pas/SubscriptionTopic/PASSubscriptionTopic";
    public const string BackportFilterCriteria = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-filter-criteria";
    public const string BackportPayloadContent = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-payload-content";

    // priority values the EHR sends
    public const string PriorityRoutine = "routine";
    public const string PriorityUrgent = "urgent";

    // request type and patient type values the EHR sends (header of the authorization request form)
    public const string RequestTypeNew = "new";
    public const string RequestTypeRenewal = "renewal";
    public const string PatientTypePermanent = "permanent";
    public const string PatientTypeTransient = "transient";

    // EHR write-back status
    public const string EhrSyncSent = "SENT";
    public const string EhrSyncFailed = "FAILED";
}
