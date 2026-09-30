using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Filters;

/// <summary>
/// Protects the EHR-facing endpoints: the X-Api-Key header must equal Security:EhrApiKey.
/// An empty key in appsettings switches the check off (local testing).
/// Applied with [ServiceFilter(typeof(EhrApiKeyFilter))]. Production: replace with OAuth client credentials.
/// </summary>
public class EhrApiKeyFilter : IActionFilter
{
    private readonly SecuritySettings _security;

    /// <summary>Created by dependency injection.</summary>
    public EhrApiKeyFilter(IOptions<SecuritySettings> security)
    {
        _security = security.Value;
    }

    /// <summary>Rejects the call with 401 when the key is missing or wrong.</summary>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrEmpty(_security.EhrApiKey))
        {
            return;
        }

        string sent = context.HttpContext.Request.Headers[ApiHeaders.EhrApiKey].ToString();
        if (!SecretCompare.AreEqual(sent, _security.EhrApiKey))
        {
            ApiError error = new ApiError();
            error.Error = "Missing or invalid " + ApiHeaders.EhrApiKey + " header.";
            error.TraceId = context.HttpContext.TraceIdentifier;
            context.Result = new UnauthorizedObjectResult(error);
        }
    }

    /// <summary>Nothing to do after the action.</summary>
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
