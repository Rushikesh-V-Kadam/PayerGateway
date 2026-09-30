namespace PayerGateway.Api.Exceptions;

/// <summary>The Provider FHIR API failed (not reachable, or returned an error other than 404).</summary>
public class FhirApiException : Exception
{
    /// <summary>Creates the exception.</summary>
    public FhirApiException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with the original error.</summary>
    public FhirApiException(string message, Exception inner) : base(message, inner)
    {
    }
}
