using Microsoft.AspNetCore.Mvc;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Security;

namespace PayerGateway.Api.Controllers;

/// <summary>Public endpoints: the gateway's public signing key, a health check, and "/" -> Swagger.</summary>
[ApiController]
public class WellKnownController : ControllerBase
{
    private readonly JwtSigner _signer;

    /// <summary>Created by dependency injection.</summary>
    public WellKnownController(JwtSigner signer)
    {
        _signer = signer;
    }

    /// <summary>JSON Web Key Set of the gateway.</summary>
    /// <remarks>
    /// Payers download it to check the JWTs the gateway signs (CDS Hooks calls, SMART Backend Services client assertions).
    /// Register this URL with each payer.
    /// </remarks>
    /// <response code="200">The public key set.</response>
    [HttpGet("/.well-known/jwks.json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult Jwks()
    {
        return Ok(_signer.GetJwks());
    }

    /// <summary>Simple "is the gateway running" check.</summary>
    /// <response code="200">The gateway is running.</response>
    [HttpGet("/health")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    public ActionResult<HealthStatus> Health()
    {
        HealthStatus status = new HealthStatus();
        status.Status = "ok";
        status.Time = FhirDate.Now();
        return Ok(status);
    }

    /// <summary>Opens Swagger UI when the root URL is browsed.</summary>
    [HttpGet("/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Root()
    {
        return Redirect("/swagger");
    }
}
