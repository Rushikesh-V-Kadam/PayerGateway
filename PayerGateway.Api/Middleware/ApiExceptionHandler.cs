using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Middleware;

/// <summary>
/// Turns every exception into one JSON error format, so controllers need no try/catch:
///   ApiException            -> its status (400 / 404 / 409 / 422 / 502), with its Body or { error, details }
///   PayerException          -> 502 (payer failed) or 504 (payer timed out)
///   FhirApiException        -> 502 (Provider FHIR API failed)
///   EhrException            -> 502 (EHR write-back failed)
///   OracleException         -> 503 (database not available)
///   BadHttpRequestException -> 400 (body could not be read)
///   JsonException           -> 400 (invalid JSON)
///   request aborted         -> nothing written (the caller is gone)
///   anything else           -> 500
/// Every error body carries the traceId that is also written to the log.
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    /// <summary>Created by dependency injection.</summary>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>Writes the error response. Returns true = handled.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // The caller disconnected: nothing to send
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request {Path} was cancelled by the caller", context.Request.Path);
            return true;
        }

        int status;
        object body;
        ApiError error = new ApiError();
        error.TraceId = context.TraceIdentifier;

        ApiException? api = exception as ApiException;
        PayerException? payer = exception as PayerException;

        if (api != null)
        {
            status = api.StatusCode;
            error.Error = api.Message;
            error.Details.AddRange(api.Details);
            if (api.Body != null)
            {
                body = api.Body;
            }
            else
            {
                body = error;
            }
            _logger.LogInformation("Request {Path} returned {Status}: {Message}", context.Request.Path, status, api.Message);
        }
        else if (payer != null)
        {
            status = payer.StatusCode;
            error.Error = payer.Message;
            body = error;
            _logger.LogWarning(exception, "Payer call failed ({TraceId})", context.TraceIdentifier);
        }
        else if (exception is FhirApiException)
        {
            status = StatusCodes.Status502BadGateway;
            error.Error = "Provider FHIR API error: " + exception.Message;
            body = error;
            _logger.LogWarning(exception, "Provider FHIR API call failed ({TraceId})", context.TraceIdentifier);
        }
        else if (exception is EhrException)
        {
            status = StatusCodes.Status502BadGateway;
            error.Error = "EHR error: " + exception.Message;
            body = error;
            _logger.LogWarning(exception, "EHR call failed ({TraceId})", context.TraceIdentifier);
        }
        else if (exception is OracleException)
        {
            status = StatusCodes.Status503ServiceUnavailable;
            error.Error = "The database is not available. Try again later.";
            body = error;
            _logger.LogError(exception, "Oracle error ({TraceId})", context.TraceIdentifier);
        }
        else if (exception is BadHttpRequestException)
        {
            status = StatusCodes.Status400BadRequest;
            error.Error = "The request body could not be read: " + exception.Message;
            body = error;
        }
        else if (exception is JsonException)
        {
            status = StatusCodes.Status400BadRequest;
            error.Error = "Invalid JSON: " + exception.Message;
            body = error;
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            error.Error = "Unexpected error. See the gateway log with traceId " + context.TraceIdentifier + ".";
            body = error;
            _logger.LogError(exception, "Unhandled error ({TraceId})", context.TraceIdentifier);
        }

        if (context.Response.HasStarted)
        {
            // Headers are already sent: the status can no longer be changed
            return true;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        string json = JsonSerializer.Serialize(body, body.GetType(), ApiJson.Options);
        await context.Response.WriteAsync(json, cancellationToken);
        return true;
    }
}
