namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// Extensions on a primitive value, written as "_name" in FHIR JSON
/// (used for Subscription._criteria and Subscription.channel._payload).
/// </summary>
public class PrimitiveExtensions
{
    public List<Extension> Extension { get; set; } = new();
}
