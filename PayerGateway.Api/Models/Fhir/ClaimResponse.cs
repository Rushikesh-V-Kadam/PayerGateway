namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR ClaimResponse (PAS profile-claimresponse): the payer's decision.</summary>
public class ClaimResponse
{
    public string ResourceType { get; set; } = "ClaimResponse";
    public string? Id { get; set; }
    public string? Created { get; set; }
    /// <summary>queued | complete | error | partial</summary>
    public string? Outcome { get; set; }
    /// <summary>Authorization number for the whole request.</summary>
    public string? PreAuthRef { get; set; }
    public Period? PreAuthPeriod { get; set; }
    public List<ClaimResponseItem> Item { get; set; } = new();
    public List<ClaimResponseError> Error { get; set; } = new();
    public List<ClaimResponseProcessNote> ProcessNote { get; set; } = new();
}
