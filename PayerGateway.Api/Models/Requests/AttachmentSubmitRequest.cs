namespace PayerGateway.Api.Models.Requests;

/// <summary>
/// Body of POST /api/v1/cdex/requests/{requestId}/attachments: additional documents and DTR forms for a pended
/// prior auth (CDex $submit-attachment), usually to answer a payer information request.
/// </summary>
public class AttachmentSubmitRequest
{
    /// <summary>Tracking id of the payer's information request. Empty = the latest open one, or unsolicited.</summary>
    /// <example>TRK-88421</example>
    public string? TrackingId { get; set; }

    /// <summary>EHR document ids (DocumentReference ids at the Provider FHIR API).</summary>
    /// <example>["doc-7002"]</example>
    public List<string> DocumentIds { get; set; } = new();

    /// <summary>QuestionnaireResponse ids (from POST /api/v1/dtr/forms/{formId}/answers).</summary>
    /// <example>[]</example>
    public List<string> QuestionnaireResponseIds { get; set; } = new();

    /// <summary>Claim line numbers the attachments support. Empty = the whole request.</summary>
    /// <example>[1]</example>
    public List<int> LineNumbers { get; set; } = new();

    /// <summary>true (default) = nothing more will be sent; the payer re-reviews now.</summary>
    /// <example>true</example>
    public bool Final { get; set; } = true;
}
