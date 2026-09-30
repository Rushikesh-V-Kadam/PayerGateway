using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Filters;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Cdex;

namespace PayerGateway.Api.Controllers;

/// <summary>
/// STEP 4 - CDex (additional documents for a pended prior auth). What the payer asked for, sending documents / forms,
/// and the endpoint a payer uses to push its information requests (Tasks).
/// </summary>
[ApiController]
[Route("api/v1/cdex")]
[Produces("application/json")]
public class CdexController : ControllerBase
{
    private readonly CdexService _cdex;

    /// <summary>Created by dependency injection.</summary>
    public CdexController(CdexService cdex)
    {
        _cdex = cdex;
    }

    /// <summary>Documents / forms the payer asked for on a pended request.</summary>
    /// <remarks>
    /// Each information request lists LOINC document types (documentsNeeded) and / or questionnaires (questionnairesNeeded).
    /// For questionnaires: POST /api/v1/dtr/forms with those URLs, answer them, then send the questionnaireResponseId.
    /// </remarks>
    /// <param name="requestId">EHR prior auth request id.</param>
    /// <param name="refresh">true = search the payer's Tasks now instead of waiting for the background job.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The information requests (may be empty).</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown requestId.</response>
    /// <response code="502">The payer failed (refresh = true).</response>
    [HttpGet("requests/{requestId}/information-requests")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(List<InformationRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<List<InformationRequest>>> InformationRequests(string requestId, [FromQuery] bool refresh = false, CancellationToken ct = default)
    {
        List<InformationRequest> result = await _cdex.GetInformationRequestsAsync(requestId, refresh, ct);
        return Ok(result);
    }

    /// <summary>Sends additional documents and / or completed forms to the payer ($submit-attachment).</summary>
    /// <remarks>
    /// Use for a pended request (the first documents go with PAS). With final = true the gateway asks the payer for the
    /// new decision right away and returns it in "decision".
    /// </remarks>
    /// <param name="requestId">EHR prior auth request id.</param>
    /// <param name="request">Documents, forms and the tracking id.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">Sent to the payer.</response>
    /// <response code="400">Nothing to send, or unknown tracking id.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown requestId, document or form.</response>
    /// <response code="422">No member id, or the payer rejected the attachments (body = result with messages).</response>
    /// <response code="502">The payer or the Provider FHIR API failed.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("requests/{requestId}/attachments")]
    [Consumes("application/json")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(AttachmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(AttachmentResult), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AttachmentResult>> SubmitAttachments(string requestId, [FromBody] AttachmentSubmitRequest request, CancellationToken ct)
    {
        AttachmentResult result = await _cdex.SubmitAttachmentsAsync(requestId, request, ct);
        return Ok(result);
    }

    /// <summary>Called BY THE PAYER to push a Task (or a Bundle of Tasks) asking for more information.</summary>
    /// <remarks>No EHR key. The payer sends "Authorization: Bearer {NotificationSecret}" when a secret is configured for it.</remarks>
    /// <param name="payerName">Payer name.</param>
    /// <param name="body">A FHIR Task or a Bundle of Tasks.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="202">Accepted.</response>
    /// <response code="400">Not a Task or Bundle.</response>
    /// <response code="401">Wrong notification secret.</response>
    /// <response code="404">The payer is not configured.</response>
    /// <response code="422">The Task is not about a request sent by this gateway.</response>
    [HttpPost("tasks/{payerName}")]
    [Consumes(ApiHeaders.FhirJson, ApiHeaders.Json)]
    [ProducesResponseType(typeof(NotificationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<NotificationResult>> ReceiveTasks(string payerName, [FromBody] JsonObject? body, CancellationToken ct)
    {
        NotificationResult result = await _cdex.ReceivePushedTasksAsync(payerName, Request.Headers.Authorization.ToString(), body, ct);
        return Accepted(result);
    }
}
