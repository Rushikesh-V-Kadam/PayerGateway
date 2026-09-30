using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Filters;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Pas;

namespace PayerGateway.Api.Controllers;

/// <summary>
/// STEP 3 - PAS (prior authorization requests): submit, update, cancel, read, inquire, plus the endpoint the payer
/// calls when a pended decision changes.
/// </summary>
[ApiController]
[Route("api/v1/pas")]
[Produces("application/json")]
public class PasController : ControllerBase
{
    private readonly PasService _pas;

    /// <summary>Created by dependency injection.</summary>
    public PasController(PasService pas)
    {
        _pas = pas;
    }

    /// <summary>Submits a new prior authorization request (Claim/$submit).</summary>
    /// <remarks>
    /// Call when order-sign returned callPas = true (after DTR when callDtr was also true).
    /// The gateway builds the PAS Bundle with the Claim, patient, coverage, organization, practitioner and orders,
    /// attaches **completed DTR forms of these orders automatically**, plus the documents in documentIds (file embedded).
    ///
    /// overallStatus in the answer: approved | partially-approved | denied | pended | not-required | contact-payer.
    /// When pended, the payer may ask for more (informationRequests) → CDex attachments.
    /// Nothing is saved when the payer rejects the request (422 / 502 with the payer's errors).
    /// </remarks>
    /// <param name="request">The prior authorization request.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="201">Accepted by the payer; the decision (may be pended).</response>
    /// <response code="400">Invalid body, missing field, unknown payerName, or an order of another patient.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Patient, order, organization, form or document not found at the Provider FHIR API.</response>
    /// <response code="409">requestId was already submitted (use PUT to change it).</response>
    /// <response code="422">No active insurance, organization without NPI, or the payer rejected the request (body = decision with errors).</response>
    /// <response code="502">The payer or the Provider FHIR API failed.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("requests")]
    [Consumes("application/json")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PriorAuthDecision>> Submit([FromBody] PriorAuthRequest request, CancellationToken ct)
    {
        PriorAuthDecision decision = await _pas.SubmitAsync(request, false, ct);
        return StatusCode(StatusCodes.Status201Created, decision);
    }

    /// <summary>Changes a request: add / remove orders, add forms or documents (Claim update).</summary>
    /// <remarks>Send the full list of orders that should stay in the request; orders left out are sent as cancelled lines.</remarks>
    /// <param name="requestId">EHR request id, e.g. PA-2026-0001.</param>
    /// <param name="request">The changed request (requestId in the body may be empty or must match the URL).</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The payer's new decision.</response>
    /// <response code="400">Invalid body, ids do not match, or patient / payer changed.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown requestId, or data not found at the Provider FHIR API.</response>
    /// <response code="409">The request is cancelled.</response>
    /// <response code="422">The payer rejected the update (body = decision with errors).</response>
    /// <response code="502">The payer or the Provider FHIR API failed.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPut("requests/{requestId}")]
    [Consumes("application/json")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PriorAuthDecision>> Update(string requestId, [FromBody] PriorAuthRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.RequestId) && request.RequestId != requestId)
        {
            ApiError error = new ApiError("requestId in the body does not match the URL.");
            error.TraceId = HttpContext.TraceIdentifier;
            return BadRequest(error);
        }
        request.RequestId = requestId;
        PriorAuthDecision decision = await _pas.SubmitAsync(request, true, ct);
        return Ok(decision);
    }

    /// <summary>Cancels the whole request (every line sent to the payer as cancelled).</summary>
    /// <param name="requestId">EHR request id.</param>
    /// <param name="cancel">Optional reason.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The cancelled decision.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown requestId.</response>
    /// <response code="422">The request's data can no longer be read, or the payer rejected the cancel.</response>
    /// <response code="502">The payer failed.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("requests/{requestId}/cancel")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PriorAuthDecision>> Cancel(string requestId, [FromBody] CancelPriorAuthRequest? cancel, CancellationToken ct)
    {
        CancelPriorAuthRequest input = cancel ?? new CancelPriorAuthRequest();
        PriorAuthDecision decision = await _pas.CancelAsync(requestId, input, ct);
        return Ok(decision);
    }

    /// <summary>Latest stored decision (no payer call).</summary>
    /// <param name="requestId">EHR request id.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The stored decision.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown requestId.</response>
    [HttpGet("requests/{requestId}")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriorAuthDecision>> Get(string requestId, CancellationToken ct)
    {
        PriorAuthDecision decision = await _pas.GetAsync(requestId, ct);
        return Ok(decision);
    }

    /// <summary>Asks the payer for the latest decision now (Claim/$inquire).</summary>
    /// <remarks>The background job also does this every Workflow:PollMinutes for pended requests.</remarks>
    /// <param name="requestId">EHR request id.</param>
    /// <param name="writeBack">true (default) = PUT the decision to the EHR when it changed.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The latest decision.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown requestId.</response>
    /// <response code="502">The payer failed.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("requests/{requestId}/inquire")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(PriorAuthDecision), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PriorAuthDecision>> Inquire(string requestId, [FromQuery] bool writeBack = true, CancellationToken ct = default)
    {
        PriorAuthDecision decision = await _pas.InquireAsync(requestId, writeBack, ct);
        return Ok(decision);
    }

    /// <summary>Called BY THE PAYER (PAS Subscription rest-hook) when a decision changes.</summary>
    /// <remarks>
    /// No EHR key. The payer sends "Authorization: Bearer {NotificationSecret}" when a secret is configured for it.
    /// The gateway then inquires every open request of that payer and writes changed decisions to the EHR.
    /// </remarks>
    /// <param name="payerName">Payer name (part of the Subscription endpoint the gateway registered).</param>
    /// <param name="body">Subscription notification Bundle.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">Handled.</response>
    /// <response code="401">Wrong notification secret.</response>
    /// <response code="404">The payer is not configured.</response>
    [HttpPost("notifications/{payerName}")]
    [Consumes(ApiHeaders.FhirJson, ApiHeaders.Json)]
    [ProducesResponseType(typeof(NotificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResult>> Notification(string payerName, [FromBody] JsonObject? body, CancellationToken ct)
    {
        NotificationResult result = await _pas.HandleNotificationAsync(payerName, Request.Headers.Authorization.ToString(), body, ct);
        return Ok(result);
    }
}
