using System.Text.Json.Nodes;
using PayerGateway.Api.Models.Fhir;

namespace PayerGateway.Api.Services.Payers;

/// <summary>Result of a FHIR call to a payer: HTTP status, the body, and the OperationOutcome when the payer sent one.</summary>
public class PayerCallResult
{
    public int StatusCode { get; set; }

    /// <summary>Response body as raw JSON (null when empty or not JSON).</summary>
    public JsonObject? Body { get; set; }

    /// <summary>The body when it is an OperationOutcome (errors or information).</summary>
    public OperationOutcome? Outcome { get; set; }

    /// <summary>True for HTTP 2xx.</summary>
    public bool IsSuccess()
    {
        return StatusCode >= 200 && StatusCode < 300;
    }

    /// <summary>Payer messages, or a default text with the HTTP status.</summary>
    public List<string> Messages()
    {
        if (Outcome != null)
        {
            List<string> messages = Outcome.Messages();
            if (messages.Count > 0)
            {
                return messages;
            }
        }
        List<string> fallback = new List<string>();
        fallback.Add("The payer returned HTTP " + StatusCode + ".");
        return fallback;
    }
}
