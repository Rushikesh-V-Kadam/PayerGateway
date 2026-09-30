namespace PayerGateway.Api.Models.Fhir;

/// <summary>Task.restriction: the due date is in period.end.</summary>
public class TaskRestriction
{
    public Period? Period { get; set; }
}
