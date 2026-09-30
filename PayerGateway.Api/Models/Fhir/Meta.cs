namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Meta: the profiles a resource claims to follow.</summary>
public class Meta
{
    public List<string> Profile { get; set; } = new();
    public string? LastUpdated { get; set; }
}
