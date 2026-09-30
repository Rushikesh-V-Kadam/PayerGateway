namespace PayerGateway.Api.Models.Responses;

/// <summary>Error body returned by every endpoint for 400 / 401 / 404 / 409 / 422 / 500 / 502 / 503 / 504.</summary>
public class ApiError
{
    /// <summary>What went wrong, in one sentence.</summary>
    /// <example>Payer 'UnknownPayer' is not configured. Configured payers: MockPayer.</example>
    public string Error { get; set; } = string.Empty;

    /// <summary>Extra messages (validation problems, payer OperationOutcome issues, missing required questions).</summary>
    /// <example>[]</example>
    public List<string> Details { get; set; } = new();

    /// <summary>Id of this request in the gateway log (give it to support).</summary>
    /// <example>0HN6Q2L3J7K1B:00000001</example>
    public string? TraceId { get; set; }

    /// <summary>Empty error (used by the JSON serializer).</summary>
    public ApiError()
    {
    }

    /// <summary>Creates an error with a message.</summary>
    public ApiError(string error)
    {
        Error = error;
    }
}
