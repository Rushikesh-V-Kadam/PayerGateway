using PayerGateway.Api.Constants;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Services.Cdex;

/// <summary>
/// Reads a payer information request (PAS / CDex Task) into the EHR's InformationRequest:
/// the tracking id, due date, LOINC document types needed and questionnaires needed.
/// </summary>
public static class TaskReader
{
    /// <summary>The payer tracking id identifier (type tracking-id), else the first identifier, else null.</summary>
    public static Identifier? TrackingIdentifier(FhirTask task)
    {
        foreach (Identifier identifier in task.Identifier)
        {
            if (identifier.Type != null && identifier.Type.HasCode(CdexCodes.TrackingIdType))
            {
                return identifier;
            }
        }
        if (task.Identifier.Count > 0)
        {
            return task.Identifier[0];
        }
        return null;
    }

    /// <summary>Our request id the Task is about (Task.reasonReference / focus identifier = our Claim identifier).</summary>
    public static string? RequestId(FhirTask task)
    {
        if (task.ReasonReference != null && task.ReasonReference.Identifier != null && task.ReasonReference.Identifier.Value != null)
        {
            return task.ReasonReference.Identifier.Value;
        }
        if (task.Focus != null && task.Focus.Identifier != null)
        {
            return task.Focus.Identifier.Value;
        }
        return null;
    }

    /// <summary>Task -> InformationRequest.</summary>
    public static InformationRequest ToInformationRequest(FhirTask task)
    {
        InformationRequest info = new InformationRequest();
        Identifier? tracking = TrackingIdentifier(task);
        info.TrackingId = tracking != null && tracking.Value != null ? tracking.Value : string.Empty;
        info.TaskId = task.Id;
        info.DueDate = task.Restriction != null && task.Restriction.Period != null ? task.Restriction.Period.End : null;
        info.Status = task.Status ?? "requested";

        foreach (TaskInput input in task.Input)
        {
            string? code = input.Type.FirstCode();
            int? line = LineNumber(input);

            if (code == PasCodes.AttachmentsNeeded)
            {
                // a document type, as a LOINC code
                Coding? loinc = null;
                if (input.ValueCodeableConcept != null)
                {
                    loinc = input.ValueCodeableConcept.CodingWithSystem(CodeSystems.Loinc) ?? input.ValueCodeableConcept.FirstCoding();
                }
                if (loinc != null && loinc.Code != null)
                {
                    DocumentNeeded needed = new DocumentNeeded();
                    needed.LoincCode = loinc.Code;
                    needed.Display = loinc.Display;
                    needed.LineNumber = line;
                    info.DocumentsNeeded.Add(needed);
                }
            }
            else if (code == PasCodes.QuestionnairesNeeded || code == "questionnaire")
            {
                // a payer questionnaire to complete with DTR (PAS sends an identifier, CDex a canonical)
                string? url = input.ValueCanonical;
                if (url == null && input.ValueIdentifier != null)
                {
                    url = input.ValueIdentifier.Value;
                }
                if (url != null && !info.QuestionnairesNeeded.Contains(url))
                {
                    info.QuestionnairesNeeded.Add(url);
                }
            }
        }
        return info;
    }

    /// <summary>The claim line a Task input applies to (paLineNumber or serviceLineNumber), or null.</summary>
    private static int? LineNumber(TaskInput input)
    {
        Extension? paLine = Extension.Find(input.Extension, PasExtensions.PaLineNumber);
        if (paLine != null && paLine.ValueInteger != null)
        {
            return paLine.ValueInteger;
        }
        Extension? serviceLine = Extension.Find(input.Extension, PasExtensions.ServiceLineNumber);
        if (serviceLine != null)
        {
            return serviceLine.ValuePositiveInt;
        }
        return null;
    }
}
