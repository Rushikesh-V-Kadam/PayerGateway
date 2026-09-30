namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Quantity: an amount with an optional unit.</summary>
public class Quantity
{
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string? System { get; set; }
    public string? Code { get; set; }
}
