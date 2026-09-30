namespace PayerGateway.Api.Models.Fhir;

/// <summary>
/// Typed view of an order: ServiceRequest, DeviceRequest or MedicationRequest (fields of all three in one class,
/// only the ones the gateway reads). The code is in code, codeCodeableConcept or medicationCodeableConcept
/// depending on the type - use GetCode().
/// </summary>
public class OrderResource
{
    public string ResourceType { get; set; } = string.Empty;
    public string? Id { get; set; }
    public ResourceReference? Subject { get; set; }
    public CodeableConcept? Code { get; set; }
    public CodeableConcept? CodeCodeableConcept { get; set; }
    public CodeableConcept? MedicationCodeableConcept { get; set; }
    public Quantity? QuantityQuantity { get; set; }
    public string? OccurrenceDateTime { get; set; }
    public Period? OccurrencePeriod { get; set; }
    public ResourceReference? Requester { get; set; }
    public List<CodeableConcept> ReasonCode { get; set; } = new();
    public List<ResourceReference> Insurance { get; set; } = new();
    public List<CodeableConcept> LocationCode { get; set; } = new();
    public MedicationDispense? DispenseRequest { get; set; }

    /// <summary>What is ordered (CPT / HCPCS / RxNorm), whatever the order type.</summary>
    public CodeableConcept? GetCode()
    {
        if (Code != null)
        {
            return Code;
        }
        if (CodeCodeableConcept != null)
        {
            return CodeCodeableConcept;
        }
        return MedicationCodeableConcept;
    }

    /// <summary>Requested quantity (dispense quantity for medications).</summary>
    public decimal? GetQuantity()
    {
        if (QuantityQuantity != null && QuantityQuantity.Value != null)
        {
            return QuantityQuantity.Value;
        }
        if (DispenseRequest != null && DispenseRequest.Quantity != null)
        {
            return DispenseRequest.Quantity.Value;
        }
        return null;
    }

    /// <summary>"Type/id" of this order, e.g. "ServiceRequest/ord-1001".</summary>
    public string GetReference()
    {
        return ResourceType + "/" + Id;
    }

    /// <summary>Id of the first Coverage in insurance, or null.</summary>
    public string? GetCoverageId()
    {
        if (Insurance.Count == 0)
        {
            return null;
        }
        return Insurance[0].GetId();
    }

    /// <summary>Id of the ordering practitioner, or null.</summary>
    public string? GetRequesterId()
    {
        if (Requester == null)
        {
            return null;
        }
        return Requester.GetId();
    }

    /// <summary>Id of the patient the order is for, or null.</summary>
    public string? GetPatientId()
    {
        if (Subject == null)
        {
            return null;
        }
        return Subject.GetId();
    }
}
