namespace PayerGateway.Api.Models.Fhir;

/// <summary>FHIR Attachment: a file, embedded (data) or linked (url).</summary>
public class Attachment
{
    public string? ContentType { get; set; }
    public string? Data { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
}
