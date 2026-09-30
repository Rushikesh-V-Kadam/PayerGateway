namespace PayerGateway.Api.Exceptions;

/// <summary>A payer could not be reached, timed out, or returned an unusable answer.</summary>
public class PayerException : Exception
{
    /// <summary>504 for a timeout, 502 otherwise.</summary>
    public int StatusCode { get; }

    /// <summary>Creates the exception.</summary>
    public PayerException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>Creates the exception with the original error.</summary>
    public PayerException(string message, int statusCode, Exception inner) : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
