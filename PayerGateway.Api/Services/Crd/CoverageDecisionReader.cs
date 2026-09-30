using PayerGateway.Api.Constants;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.CdsHooks;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Services.Crd;

/// <summary>
/// Reads the payer's CRD answer. The payer returns each order in a systemAction "update" with the CRD 2.0.1
/// coverage-information extension (covered, pa-needed, doc-needed, questionnaire ...). This class turns that
/// extension into the EHR's CoverageDecision, and sets callDtr / callPas.
/// </summary>
public static class CoverageDecisionReader
{
    /// <summary>"Type/id" of each order -> its decision.</summary>
    public static Dictionary<string, CoverageDecision> Read(CdsHookResponse response)
    {
        Dictionary<string, CoverageDecision> decisions = new Dictionary<string, CoverageDecision>();

        foreach (CdsAction action in response.SystemActions)
        {
            if (action.Resource == null)
            {
                continue;
            }
            string reference = FhirJson.ResourceType(action.Resource) + "/" + FhirJson.Id(action.Resource);
            List<Extension>? extensions = FhirJson.Read<List<Extension>>(action.Resource["extension"]);
            if (extensions == null)
            {
                continue;
            }

            foreach (Extension info in extensions)
            {
                if (info.Url != CrdCodes.CoverageInformationUrl)
                {
                    continue;
                }
                CoverageDecision decision = ToDecision(info);

                // With several coverages the payer may send several extensions: keep the strictest answer
                CoverageDecision? existing;
                if (!decisions.TryGetValue(reference, out existing) || Strictness(decision) > Strictness(existing))
                {
                    decisions[reference] = decision;
                }
            }
        }
        return decisions;
    }

    /// <summary>
    /// What the EHR should do next:
    ///   callDtr = the payer wants documentation (doc-needed clinical / admin / both / conditional) or listed a questionnaire
    ///   callPas = the payer says prior auth is needed (pa-needed = auth-needed)
    /// Nothing is called for a not-covered order.
    /// </summary>
    public static void SetNextSteps(CoverageDecision decision)
    {
        if (decision.Covered == CrdCodes.NotCovered)
        {
            decision.CallDtr = false;
            decision.CallPas = false;
            return;
        }

        bool documentationNeeded = !string.IsNullOrEmpty(decision.DocumentationNeeded) && decision.DocumentationNeeded != CrdCodes.NoDoc;
        decision.CallDtr = documentationNeeded || decision.QuestionnaireUrls.Count > 0;
        decision.CallPas = decision.PriorAuthNeeded == CrdCodes.AuthNeeded;
    }

    /// <summary>One coverage-information extension -> CoverageDecision.</summary>
    private static CoverageDecision ToDecision(Extension info)
    {
        CoverageDecision decision = new CoverageDecision();

        Extension? coverage = info.Child(CrdCodes.PartCoverage);
        if (coverage != null && coverage.ValueReference != null)
        {
            decision.CoverageId = coverage.ValueReference.GetId();
        }

        Extension? covered = info.Child(CrdCodes.PartCovered);
        decision.Covered = covered != null && covered.ValueCode != null ? covered.ValueCode : CrdCodes.Conditional;

        Extension? paNeeded = info.Child(CrdCodes.PartPaNeeded);
        decision.PriorAuthNeeded = paNeeded == null ? null : paNeeded.ValueCode;

        Extension? docNeeded = info.Child(CrdCodes.PartDocNeeded);
        decision.DocumentationNeeded = docNeeded == null ? null : docNeeded.ValueCode;

        Extension? assertion = info.Child(CrdCodes.PartCoverageAssertionId);
        decision.CoverageAssertionId = assertion == null ? null : assertion.ValueString;

        Extension? satisfied = info.Child(CrdCodes.PartSatisfiedPaId);
        decision.SatisfiedAuthorizationNumber = satisfied == null ? null : satisfied.ValueString;

        Extension? date = info.Child(CrdCodes.PartDate);
        decision.DecidedAt = date != null && date.ValueDate != null ? date.ValueDate : FhirDate.Now();

        foreach (Extension part in info.Children)
        {
            if (part.Url == CrdCodes.PartDocPurpose && part.ValueCode != null)
            {
                decision.DocumentationPurpose.Add(part.ValueCode);
            }
            else if (part.Url == CrdCodes.PartInfoNeeded && part.ValueCode != null)
            {
                decision.InformationNeeded.Add(part.ValueCode);
            }
            else if (part.Url == CrdCodes.PartQuestionnaire && part.ValueCanonical != null)
            {
                if (!decision.QuestionnaireUrls.Contains(part.ValueCanonical))
                {
                    decision.QuestionnaireUrls.Add(part.ValueCanonical);
                }
            }
            else if (part.Url == CrdCodes.PartReason && part.ValueCodeableConcept != null)
            {
                string? reason = part.ValueCodeableConcept.BestText();
                if (reason != null)
                {
                    EhrMessage message = new EhrMessage();
                    message.Severity = "info";
                    message.Summary = TextHelper.Shorten(reason, 140);
                    message.Detail = reason;
                    decision.Messages.Add(message);
                }
            }
        }
        return decision;
    }

    /// <summary>not-covered (4) &gt; auth needed (3) &gt; conditional (2) &gt; anything else (1).</summary>
    private static int Strictness(CoverageDecision decision)
    {
        if (decision.Covered == CrdCodes.NotCovered)
        {
            return 4;
        }
        if (decision.PriorAuthNeeded == CrdCodes.AuthNeeded || decision.PriorAuthNeeded == CrdCodes.PerformPa)
        {
            return 3;
        }
        if (decision.Covered == CrdCodes.Conditional || decision.PriorAuthNeeded == CrdCodes.Conditional)
        {
            return 2;
        }
        return 1;
    }
}
