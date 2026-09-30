using System.Text.Json.Nodes;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Fhir;

namespace PayerGateway.Api.Services;

/// <summary>
/// Checks shared by CRD, DTR and PAS: every requested order exists and belongs to the patient, and which Coverage to use.
/// </summary>
public static class OrderHelper
{
    /// <summary>Typed view of a raw order (never null: an empty OrderResource when it cannot be read).</summary>
    public static OrderResource ReadOrder(JsonObject raw)
    {
        OrderResource? order = FhirJson.Read<OrderResource>(raw);
        if (order == null)
        {
            return new OrderResource();
        }
        return order;
    }

    /// <summary>The raw order with this id, or null.</summary>
    public static JsonObject? FindOrder(List<JsonObject> orders, string orderId)
    {
        foreach (JsonObject raw in orders)
        {
            if (FhirJson.Id(raw) == orderId)
            {
                return raw;
            }
        }
        return null;
    }

    /// <summary>
    /// Every requested order must have been found and must belong to the patient.
    /// Missing order -> 404, order of another patient -> 400.
    /// </summary>
    public static void CheckOrders(List<string> requestedIds, List<JsonObject> orders, string patientId)
    {
        foreach (string orderId in TextHelper.Distinct(requestedIds))
        {
            JsonObject? raw = FindOrder(orders, orderId);
            if (raw == null)
            {
                throw new ApiException(404, "Order " + orderId + " was not found at the Provider FHIR API.");
            }
            OrderResource order = ReadOrder(raw);
            if (order.GetPatientId() != patientId)
            {
                throw new ApiException(400, "Order " + orderId + " does not belong to patient " + patientId + ".");
            }
        }
    }

    /// <summary>The first order's Coverage id (Order.insurance), or null.</summary>
    public static string? CoverageIdOfOrders(List<JsonObject> orders)
    {
        foreach (JsonObject raw in orders)
        {
            string? coverageId = ReadOrder(raw).GetCoverageId();
            if (coverageId != null)
            {
                return coverageId;
            }
        }
        return null;
    }

    /// <summary>The first order's requester (ordering practitioner) id, or null.</summary>
    public static string? RequesterOfOrders(List<JsonObject> orders)
    {
        foreach (JsonObject raw in orders)
        {
            string? requesterId = ReadOrder(raw).GetRequesterId();
            if (requesterId != null)
            {
                return requesterId;
            }
        }
        return null;
    }

    /// <summary>
    /// Which Coverage to use: the one asked for, else the orders' coverage, else the first active one. Null when none.
    /// </summary>
    public static Coverage? ChooseCoverage(List<Coverage> activeCoverages, string? requestedCoverageId, List<JsonObject> orders)
    {
        string? coverageId = requestedCoverageId;
        if (string.IsNullOrWhiteSpace(coverageId))
        {
            coverageId = CoverageIdOfOrders(orders);
        }

        if (!string.IsNullOrWhiteSpace(coverageId))
        {
            foreach (Coverage coverage in activeCoverages)
            {
                if (coverage.Id == coverageId)
                {
                    return coverage;
                }
            }
            return null;
        }

        if (activeCoverages.Count > 0)
        {
            return activeCoverages[0];
        }
        return null;
    }
}
