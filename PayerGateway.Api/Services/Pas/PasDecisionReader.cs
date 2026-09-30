using System.Text.Json.Nodes;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Services.Pas;

/// <summary>
/// Reads the payer's PAS answer (ClaimResponse) and turns it into the EHR's PriorAuthDecision:
///   per line  : extension-reviewAction (X12 306 code A1/A2/A3/A4..., authorization number, reason), authorized quantity/period
///   overall   : preAuthRef / preAuthPeriod, and a combined status from the lines
///   errors    : ClaimResponse.error, or the OperationOutcome of a rejected request
/// </summary>
public static class PasDecisionReader
{
    /// <summary>The newest ClaimResponse in a payer response Bundle, or null.</summary>
    public static ClaimResponse? FindClaimResponse(PayerCallResult result)
    {
        if (FhirJson.ResourceType(result.Body) != "Bundle")
        {
            return null;
        }
        Bundle? bundle = FhirJson.Read<Bundle>(result.Body);
        if (bundle == null)
        {
            return null;
        }

        ClaimResponse? newest = null;
        foreach (JsonObject raw in bundle.ResourcesOfType("ClaimResponse"))
        {
            ClaimResponse? response = FhirJson.Read<ClaimResponse>(raw);
            if (response == null)
            {
                continue;
            }
            if (newest == null || string.CompareOrdinal(response.Created ?? string.Empty, newest.Created ?? string.Empty) > 0)
            {
                newest = response;
            }
        }
        return newest;
    }

    /// <summary>Builds the decision from a ClaimResponse.</summary>
    /// <param name="response">The payer's ClaimResponse.</param>
    /// <param name="request">The Claim we sent (for requested codes and quantities per line).</param>
    /// <param name="record">The stored request (ids, line numbers, open information requests).</param>
    /// <param name="workflow">Decision deadline settings.</param>
    public static PriorAuthDecision Read(ClaimResponse response, Claim request, PriorAuthRecord record, WorkflowSettings workflow)
    {
        PriorAuthDecision decision = new PriorAuthDecision();
        decision.RequestId = record.RequestId;
        decision.PayerName = record.PayerName;
        decision.PatientId = record.PatientId;
        decision.OrderIds = new List<string>(record.OrderIds);
        decision.InformationRequests = record.Decision.InformationRequests;   // kept up to date by CdexService

        DateTime dueBy = record.Priority == PasCodes.PriorityUrgent
            ? record.SubmittedAt.AddHours(workflow.ExpeditedDecisionHours)
            : record.SubmittedAt.AddDays(workflow.StandardDecisionDays);
        decision.DecisionDueBy = FhirDate.Instant(dueBy);

        // line number -> order id
        Dictionary<int, string> orderByLine = new Dictionary<int, string>();
        foreach (KeyValuePair<string, int> pair in record.LineNumbers)
        {
            orderByLine[pair.Value] = pair.Key;
        }

        foreach (ClaimResponseItem item in response.Item)
        {
            decision.Lines.Add(ReadLine(item, response, request, orderByLine));
            if (decision.AuthorizationNumber == null)
            {
                decision.AuthorizationNumber = ReviewNumber(item);
            }
        }
        decision.Lines.Sort(CompareLines);

        // Whole-request values win over line values
        if (response.PreAuthRef != null)
        {
            decision.AuthorizationNumber = response.PreAuthRef;
        }
        decision.ValidFrom = response.PreAuthPeriod != null && response.PreAuthPeriod.Start != null ? response.PreAuthPeriod.Start : EarliestFrom(decision.Lines);
        decision.ValidTo = response.PreAuthPeriod != null && response.PreAuthPeriod.End != null ? response.PreAuthPeriod.End : LatestTo(decision.Lines);

        foreach (ClaimResponseError error in response.Error)
        {
            string text = error.Code.BestText() ?? "Payer error";
            decision.Errors.Add(error.ItemSequence != null ? "Line " + error.ItemSequence.Value + ": " + text : text);
        }

        if (response.Outcome == "error" && decision.Lines.Count == 0)
        {
            decision.OverallStatus = DecisionStatus.Error;
        }
        else
        {
            decision.OverallStatus = Overall(decision.Lines);
        }
        if (decision.OverallStatus != DecisionStatus.Pended)
        {
            decision.DecidedAt = FhirDate.Now();
        }
        return decision;
    }

    /// <summary>Decision for a request the payer rejected (HTTP 4xx with an OperationOutcome, or no ClaimResponse).</summary>
    public static PriorAuthDecision Rejected(string requestId, string payerName, string patientId, List<string> orderIds, PayerCallResult result)
    {
        PriorAuthDecision decision = new PriorAuthDecision();
        decision.RequestId = requestId;
        decision.PayerName = payerName;
        decision.PatientId = patientId;
        decision.OrderIds = orderIds;
        decision.OverallStatus = DecisionStatus.Error;
        decision.Errors.AddRange(result.Messages());
        return decision;
    }

    /// <summary>X12 306 action code -> line decision.</summary>
    public static string LineDecision(string? x12Code)
    {
        switch (x12Code)
        {
            case X12ReviewActions.Approved:
                return DecisionStatus.Approved;
            case X12ReviewActions.PartiallyApproved:
                return DecisionStatus.PartiallyApproved;
            case X12ReviewActions.Denied:
                return DecisionStatus.Denied;
            case X12ReviewActions.Modified:
                return DecisionStatus.Modified;
            case X12ReviewActions.Cancelled:
                return DecisionStatus.Cancelled;
            case X12ReviewActions.ContactPayer:
                return DecisionStatus.ContactPayer;
            case X12ReviewActions.NotRequired:
                return DecisionStatus.NotRequired;
            default:
                return DecisionStatus.Pended;   // A4 or unknown
        }
    }

    /// <summary>
    /// Overall status from the lines (cancelled lines are ignored unless all are cancelled):
    /// any pended -> pended; all not-required -> not-required; all approved -> approved;
    /// some approved -> partially-approved; all denied -> denied; otherwise contact-payer.
    /// </summary>
    public static string Overall(List<PriorAuthLine> lines)
    {
        if (lines.Count == 0)
        {
            return DecisionStatus.Pended;
        }

        List<string> active = new List<string>();
        foreach (PriorAuthLine line in lines)
        {
            if (line.Decision != DecisionStatus.Cancelled)
            {
                active.Add(line.Decision);
            }
        }
        if (active.Count == 0) return DecisionStatus.Cancelled;
        if (active.Contains(DecisionStatus.Pended)) return DecisionStatus.Pended;

        int approved = 0;
        int notRequired = 0;
        int denied = 0;
        int partial = 0;
        foreach (string value in active)
        {
            if (value == DecisionStatus.Approved || value == DecisionStatus.Modified) approved++;
            else if (value == DecisionStatus.NotRequired) notRequired++;
            else if (value == DecisionStatus.Denied) denied++;
            else if (value == DecisionStatus.PartiallyApproved) partial++;
        }

        if (notRequired == active.Count) return DecisionStatus.NotRequired;
        if (approved + notRequired == active.Count) return DecisionStatus.Approved;
        if (approved > 0 || partial > 0) return DecisionStatus.PartiallyApproved;
        if (denied + notRequired == active.Count) return DecisionStatus.Denied;
        return DecisionStatus.ContactPayer;
    }

    // ================================================================== helpers

    /// <summary>One ClaimResponse.item -> PriorAuthLine.</summary>
    private static PriorAuthLine ReadLine(ClaimResponseItem item, ClaimResponse response, Claim request, Dictionary<int, string> orderByLine)
    {
        ClaimItem? requested = null;
        foreach (ClaimItem claimItem in request.Item)
        {
            if (claimItem.Sequence == item.ItemSequence)
            {
                requested = claimItem;
                break;
            }
        }

        Extension? review = Extension.Find(item.Extension, PasExtensions.ReviewAction);
        string? actionCode = null;
        string? reason = null;
        if (review != null)
        {
            Extension? code = review.Child(PasExtensions.ReviewActionCode);
            if (code != null && code.ValueCodeableConcept != null)
            {
                actionCode = code.ValueCodeableConcept.FirstCode();
            }
            Extension? reasonCode = review.Child("reasonCode");
            if (reasonCode != null && reasonCode.ValueCodeableConcept != null)
            {
                reason = reasonCode.ValueCodeableConcept.BestText();
            }
        }
        if (reason == null)
        {
            reason = NoteText(item, response);
        }

        PriorAuthLine line = new PriorAuthLine();
        line.Sequence = item.ItemSequence;
        string? orderId;
        if (orderByLine.TryGetValue(item.ItemSequence, out orderId))
        {
            line.OrderId = orderId;
        }
        if (requested != null && requested.ProductOrService != null)
        {
            Coding? product = requested.ProductOrService.FirstCoding();
            if (product != null)
            {
                line.Code = product.Code;
                line.CodeSystem = product.System;
            }
        }
        if (requested != null && requested.Quantity != null)
        {
            line.RequestedQuantity = requested.Quantity.Value;
        }
        line.X12ActionCode = actionCode;
        line.Decision = LineDecision(actionCode);
        line.Reason = reason;

        Extension? period = Extension.Find(item.Extension, PasExtensions.ItemPreAuthPeriod);
        if (period != null && period.ValuePeriod != null)
        {
            line.ValidFrom = period.ValuePeriod.Start;
            line.ValidTo = period.ValuePeriod.End;
        }

        Extension? authorized = Extension.Find(item.Extension, PasExtensions.ItemAuthorizedDetail);
        if (authorized != null)
        {
            Extension? quantity = authorized.Child("quantity");
            if (quantity != null && quantity.ValueQuantity != null)
            {
                line.ApprovedQuantity = quantity.ValueQuantity.Value;
            }
        }
        return line;
    }

    /// <summary>The authorization number on a line's review action, or null.</summary>
    private static string? ReviewNumber(ClaimResponseItem item)
    {
        Extension? review = Extension.Find(item.Extension, PasExtensions.ReviewAction);
        if (review == null)
        {
            return null;
        }
        Extension? number = review.Child("number");
        return number == null ? null : number.ValueString;
    }

    /// <summary>The first payer note linked to the line, or null.</summary>
    private static string? NoteText(ClaimResponseItem item, ClaimResponse response)
    {
        foreach (int noteNumber in item.NoteNumber)
        {
            foreach (ClaimResponseProcessNote note in response.ProcessNote)
            {
                if (note.Number == noteNumber && note.Text != null)
                {
                    return note.Text;
                }
            }
        }
        return null;
    }

    /// <summary>Earliest line start date, or null.</summary>
    private static string? EarliestFrom(List<PriorAuthLine> lines)
    {
        string? earliest = null;
        foreach (PriorAuthLine line in lines)
        {
            if (line.ValidFrom != null && (earliest == null || string.CompareOrdinal(line.ValidFrom, earliest) < 0))
            {
                earliest = line.ValidFrom;
            }
        }
        return earliest;
    }

    /// <summary>Latest line end date, or null.</summary>
    private static string? LatestTo(List<PriorAuthLine> lines)
    {
        string? latest = null;
        foreach (PriorAuthLine line in lines)
        {
            if (line.ValidTo != null && (latest == null || string.CompareOrdinal(line.ValidTo, latest) > 0))
            {
                latest = line.ValidTo;
            }
        }
        return latest;
    }

    /// <summary>Sort order of decision lines: by sequence number.</summary>
    private static int CompareLines(PriorAuthLine first, PriorAuthLine second)
    {
        return first.Sequence.CompareTo(second.Sequence);
    }
}
