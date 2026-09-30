namespace PayerGateway.Api.Constants;

/// <summary>DTR 2.0.1 operations, parameters, extension URLs and form status values.</summary>
public static class DtrCodes
{
    public const string QuestionnairePackage = "Questionnaire/$questionnaire-package";
    public const string NextQuestion = "Questionnaire/$next-question";
    public const string InputParametersProfile = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-qpackage-input-parameters";

    // $questionnaire-package input / output parameter names
    public const string ParamCoverage = "coverage";
    public const string ParamOrder = "order";
    public const string ParamQuestionnaire = "questionnaire";
    public const string ParamContext = "context";
    public const string ParamPackageBundle = "PackageBundle";

    /// <summary>qr-context: which order / coverage a saved form belongs to.</summary>
    public const string QrContextUrl = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/qr-context";

    /// <summary>information-origin: whether an answer was auto-filled, overridden or entered manually.</summary>
    public const string InformationOriginUrl = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/information-origin";

    /// <summary>DTR QuestionnaireResponse profile.</summary>
    public const string QuestionnaireResponseProfile = "http://hl7.org/fhir/us/davinci-dtr/StructureDefinition/dtr-questionnaireresponse";

    // adaptive forms (SDC $next-question)
    public const string AdaptiveExtensionUrl = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-adaptive";

    /// <summary>SDC 3.0 name of the same flag (sdc-questionnaire-questionnaireAdaptive); payers send either one.</summary>
    public const string AdaptiveExtensionUrlSdc3 = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-questionnaireAdaptive";
    public const string ContainedQuestionnaireId = "adaptive-questionnaire";
    public const string ParamIn = "in";
    public const string ParamReturn = "return";

    // form status (QuestionnaireResponse.status)
    public const string StatusNew = "new";
    public const string StatusInProgress = "in-progress";
    public const string StatusCompleted = "completed";
}
