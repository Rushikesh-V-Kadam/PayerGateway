namespace PayerGateway.Api.Exceptions;

/// <summary>A write-back to the EHR failed.</summary>
public class EhrException : Exception
{
    /// <summary>Creates the exception.</summary>
    public EhrException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with the original error.</summary>
    public EhrException(string message, Exception inner) : base(message, inner)
    {
    }
}
