using Microsoft.AspNetCore.Mvc;
using PayerGateway.Api.Filters;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Crd;

namespace PayerGateway.Api.Controllers;

/// <summary>STEP 1 - CRD order-sign: does the signed order need prior authorization and / or documentation?</summary>
[ApiController]
[Route("api/v1/crd")]
[Produces("application/json")]
[ServiceFilter(typeof(EhrApiKeyFilter))]
public class CrdController : ControllerBase
{
    private readonly CrdService _crd;

    /// <summary>Created by dependency injection.</summary>
    public CrdController(CrdService crd)
    {
        _crd = crd;
    }

    /// <summary>Order signed: asks the payer (CDS Hooks order-sign) and returns one coverage decision per order.</summary>
    /// <remarks>
    /// The gateway reads the patient, orders and coverage from the Provider FHIR API, calls the payer named in
    /// payerName, and reads pa-needed / doc-needed from the payer's answer.
    ///
    /// What to do with each decision:
    /// - **callDtr = true** → POST /api/v1/dtr/forms with questionnaireUrls and coverageAssertionId, then send the answers.
    /// - **callPas = true** → POST /api/v1/pas/requests (after DTR when both are true).
    /// - both false → nothing more is needed for that order.
    ///
    /// If the payer cannot be reached, the call still returns 200 with covered = "unknown" and payerCall.outcome = "timeout" / "error".
    /// Sending the same eventId again returns the stored answer without calling the payer.
    /// </remarks>
    /// <param name="request">The signing event.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">One decision per order (see callDtr / callPas).</response>
    /// <response code="400">Invalid body, missing field, unknown payerName, or an order of another patient.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Patient or orders not found at the Provider FHIR API.</response>
    /// <response code="502">The Provider FHIR API failed.</response>
    /// <response code="503">The database is not available.</response>
    [HttpPost("order-sign")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderSignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderSignResponse>> OrderSign([FromBody] OrderSignRequest request, CancellationToken ct)
    {
        OrderSignResponse response = await _crd.OrderSignAsync(request, ct);
        return Ok(response);
    }

    /// <summary>Latest coverage decision of an order (no payer call).</summary>
    /// <param name="orderId">EHR order id, e.g. ord-1001.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The stored decision.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">No order-sign was run for this order.</response>
    [HttpGet("decisions/{orderId}")]
    [ProducesResponseType(typeof(CoverageDecision), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoverageDecision>> GetDecision(string orderId, CancellationToken ct)
    {
        CoverageDecision decision = await _crd.GetDecisionAsync(orderId, ct);
        return Ok(decision);
    }
}
