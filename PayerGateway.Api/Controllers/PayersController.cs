using Microsoft.AspNetCore.Mvc;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Filters;
using PayerGateway.Api.Models.CdsHooks;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Controllers;

/// <summary>STEP 0 - Connected payers: which payerName values the EHR can send, and a connection test (CDS Hooks discovery).</summary>
[ApiController]
[Route("api/v1/payers")]
[Produces("application/json")]
[ServiceFilter(typeof(EhrApiKeyFilter))]
public class PayersController : ControllerBase
{
    private readonly PayerRegistry _payers;
    private readonly PayerClient _payerClient;

    /// <summary>Created by dependency injection.</summary>
    public PayersController(PayerRegistry payers, PayerClient payerClient)
    {
        _payers = payers;
        _payerClient = payerClient;
    }

    /// <summary>Lists the payers configured in appsettings "Payers".</summary>
    /// <remarks>Use a payerName from this list in every other request.</remarks>
    /// <response code="200">The configured payers.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<PayerSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public ActionResult<List<PayerSummary>> List()
    {
        List<PayerSummary> result = new List<PayerSummary>();
        foreach (PayerSettings payer in _payers.GetAll())
        {
            PayerSummary summary = new PayerSummary();
            summary.PayerName = payer.PayerName;
            summary.DisplayName = payer.GetDisplayName();
            summary.Enabled = payer.Enabled;
            summary.BaseUrl = payer.BaseUrl;
            summary.PayerIdentifier = payer.PayerIdentifier;
            result.Add(summary);
        }
        return Ok(result);
    }

    /// <summary>Tests a payer connection: GET {payer}/cds-services (CDS Hooks discovery).</summary>
    /// <remarks>
    /// Shows which hooks the payer supports (look for "order-sign") and its prefetch templates,
    /// and refreshes the gateway's 12-hour cache. Use it when a new payer is onboarded.
    /// </remarks>
    /// <param name="payerName">Payer name from GET /api/v1/payers, e.g. MockPayer.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The payer's CDS services.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">The payer is not configured.</response>
    /// <response code="502">The payer answered with an error.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("{payerName}/discover")]
    [ProducesResponseType(typeof(CdsDiscoveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<CdsDiscoveryResponse>> Discover(string payerName, CancellationToken ct)
    {
        PayerSettings payer = _payers.GetRequired(payerName, StatusCodes.Status404NotFound);
        CdsDiscoveryResponse discovery = await _payerClient.DiscoverAsync(payer, ct);
        _payers.CacheDiscovery(payer.PayerName, discovery);
        return Ok(discovery);
    }
}
