namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Reference, e.g. { "reference": "Patient/pat-1" } or a reference by identifier.</summary>
public class ResourceReference
{
    public string? Reference { get; set; }
    public string? Type { get; set; }
    public Identifier? Identifier { get; set; }
    public string? Display { get; set; }

    /// <summary>Empty reference (used by the JSON serializer).</summary>
    public ResourceReference()
    {
    }

    /// <summary>Creates a relative reference such as "Patient/pat-1".</summary>
    public ResourceReference(string reference)
    {
        Reference = reference;
    }

    /// <summary>The id part of "Type/id", or null.</summary>
    public string? GetId()
    {
        if (string.IsNullOrEmpty(Reference))
        {
            return null;
        }
        string[] parts = Reference.Split('/');
        return parts[parts.Length - 1];
    }

    /// <summary>The type part of "Type/id" (also for absolute URLs), or null.</summary>
    public string? GetResourceType()
    {
        if (string.IsNullOrEmpty(Reference))
        {
            return null;
        }
        string[] parts = Reference.Split('/');
        if (parts.Length < 2)
        {
            return null;
        }
        return parts[parts.Length - 2];
    }
}
