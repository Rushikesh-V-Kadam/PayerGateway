namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// FHIR Task (PAS / CDex attachment request): the payer asks for documents or forms.
/// Named FhirTask to avoid a clash with System.Threading.Tasks.Task.
/// </summary>
public class FhirTask
{
    public string ResourceType { get; set; } = "Task";
    public string? Id { get; set; }
    /// <summary>Includes the payer tracking id (type = cdex-temp#tracking-id).</summary>
    public List<Identifier> Identifier { get; set; } = new();
    public string? Status { get; set; }
    public CodeableConcept? Code { get; set; }
    public ResourceReference? Focus { get; set; }
    /// <summary>Points to our Claim by identifier (the prior auth request id).</summary>
    public ResourceReference? ReasonReference { get; set; }
    public ResourceReference? Owner { get; set; }
    public TaskRestriction? Restriction { get; set; }
    public List<TaskInput> Input { get; set; } = new();
}
