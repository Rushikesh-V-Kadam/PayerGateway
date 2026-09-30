using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Middleware;

/// <summary>
/// Response for a request body that fails model validation (invalid JSON, wrong type, missing [Required] field).
/// Returns 400 with the same ApiError format as every other error, listing each problem in "details".
/// </summary>
public static class ModelValidationResponse
{
    /// <summary>Builds the 400 response. Registered in Startup as InvalidModelStateResponseFactory.</summary>
    public static IActionResult Create(ActionContext context)
    {
        ApiError error = new ApiError();
        error.Error = "The request is not valid.";
        error.TraceId = context.HttpContext.TraceIdentifier;

        foreach (KeyValuePair<string, ModelStateEntry> entry in context.ModelState)
        {
            if (entry.Value == null)
            {
                continue;
            }
            foreach (ModelError problem in entry.Value.Errors)
            {
                string message = problem.ErrorMessage;
                if (string.IsNullOrEmpty(message) && problem.Exception != null)
                {
                    message = problem.Exception.Message;
                }
                if (string.IsNullOrEmpty(entry.Key))
                {
                    error.Details.Add(message);
                }
                else
                {
                    error.Details.Add(entry.Key + ": " + message);
                }
            }
        }

        return new BadRequestObjectResult(error);
    }
}
