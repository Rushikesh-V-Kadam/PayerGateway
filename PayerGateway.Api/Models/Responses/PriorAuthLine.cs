namespace PayerGateway.Api.Models.Responses;

/// <summary>Decision for one claim line (one order).</summary>
public class PriorAuthLine
{
    /// <summary>Claim.item.sequence. Stays the same for an order across updates.</summary>
    /// <example>1</example>
    public int Sequence { get; set; }

    /// <example>ord-1001</example>
    public string? OrderId { get; set; }

    /// <example>90935</example>
    public string? Code { get; set; }

    /// <example>http://www.ama-assn.org/go/cpt</example>
    public string? CodeSystem { get; set; }

    /// <summary>approved | partially-approved | denied | pended | not-required | cancelled | contact-payer | modified</summary>
    /// <example>pended</example>
    public string Decision { get; set; } = "pended";

    /// <summary>X12 306 code from the payer: A1, A2, A3, A4, A6, C, CT, NA.</summary>
    /// <example>A4</example>
    public string? X12ActionCode { get; set; }

    public decimal? RequestedQuantity { get; set; }
    public decimal? ApprovedQuantity { get; set; }
    public string? Reason { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
}
