namespace PayerGateway.Api.Models.Fhir;

/// <summary>A free-text note from the payer.</summary>
public class ClaimResponseProcessNote
{
    public int? Number { get; set; }
    public string? Text { get; set; }
}
