using System.Text.Json.Nodes;
using PayerGateway.Api.Models.Fhir;

namespace PayerGateway.Api.Services.Pas;

/// <summary>One order that becomes one Claim.item: the raw order, its typed view, and whether the line is cancelled.</summary>
public class PasOrderLine
{
    public JsonObject Raw { get; set; } = new JsonObject();
    public OrderResource Order { get; set; } = new OrderResource();
    public bool Cancelled { get; set; }
}
