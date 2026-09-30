namespace PayerGateway.Api.Exceptions;

/// <summary>
/// A problem the EHR should see as an HTTP error (400 bad input, 404 not found, 409 conflict, 422 cannot process,
/// 502 payer failed). ApiExceptionHandler writes it as JSON: the Body when given (e.g. a decision with errors),
/// otherwise { "error": message, "details": [...] }.
/// </summary>
public class ApiException : Exception
{
    /// <summary>HTTP status to return.</summary>
    public int StatusCode { get; }

    /// <summary>Optional response body instead of the default error object.</summary>
    public object? Body { get; }

    /// <summary>Optional extra messages (shown in "details").</summary>
    public List<string> Details { get; } = new List<string>();

    /// <summary>Creates the exception.</summary>
    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>Creates the exception with a response body to return instead of the error object.</summary>
    public ApiException(int statusCode, string message, object? body) : base(message)
    {
        StatusCode = statusCode;
        Body = body;
    }

    /// <summary>Creates the exception with extra messages.</summary>
    public ApiException(int statusCode, string message, List<string> details) : base(message)
    {
        StatusCode = statusCode;
        Details.AddRange(details);
    }
}
