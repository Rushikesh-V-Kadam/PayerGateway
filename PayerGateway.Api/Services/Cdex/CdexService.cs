using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Clients;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Repositories;
using PayerGateway.Api.Services.Pas;
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Services.Cdex;

/// <summary>
/// STEP 4 - CDex (Clinical Data Exchange) attachments. Only used when the payer pends a prior auth and needs more
/// (the first clinical documents are already sent with PAS).
///
///   1. The payer creates a Task: LOINC document types and/or questionnaires needed, a tracking id and a due date.
///   2. The gateway gets the Task - by searching the payer (GET {fhir}/Task?owner={organization NPI}), or the payer
///      pushes it to /api/v1/cdex/tasks/{payerName} - adds it to the decision's informationRequests and tells the EHR.
///   3. The EHR sends the documents / completed forms -> POST {fhir}/$submit-attachment with the tracking id.
///   4. The payer reviews again; the gateway runs $inquire and returns the new decision.
/// </summary>
public class CdexService
{
    private readonly PayerRegistry _payers;
    private readonly PayerClient _payerClient;
    private readonly FhirApiClient _fhir;
    private readonly IPriorAuthRepository _requests;
    private readonly ICdexTaskRepository _tasks;
    private readonly PasService _pas;
    private readonly WorkflowSettings _workflow;
    private readonly ILogger<CdexService> _logger;

    /// <summary>Created by dependency injection.</summary>
    public CdexService(PayerRegistry payers, PayerClient payerClient, FhirApiClient fhir, IPriorAuthRepository requests,
        ICdexTaskRepository tasks, PasService pas, IOptions<WorkflowSettings> workflow, ILogger<CdexService> logger)
    {
        _payers = payers;
        _payerClient = payerClient;
        _fhir = fhir;
        _requests = requests;
        _tasks = tasks;
        _pas = pas;
        _workflow = workflow.Value;
        _logger = logger;
    }

    // ================================================================== 1-2. receive Tasks

    /// <summary>
    /// The information requests of a prior auth request. refresh = true searches the payer's Tasks now
    /// (instead of waiting for the background poller or a push).
    /// </summary>
    public async Task<List<InformationRequest>> GetInformationRequestsAsync(string requestId, bool refresh, CancellationToken ct)
    {
        PriorAuthRecord record = await GetRecordAsync(requestId, ct);
        if (refresh && record.IsOpen() && record.OrganizationNpi != null)
        {
            await PollTasksAsync(record.PayerName, record.OrganizationNpi, ct);
            record = await GetRecordAsync(requestId, ct);
        }
        return record.Decision.InformationRequests;
    }

    /// <summary>Searches the payer's Tasks for our organization and processes each one. Returns how many matched a request.</summary>
    public async Task<int> PollTasksAsync(string payerName, string organizationNpi, CancellationToken ct)
    {
        PayerSettings? payer = _payers.Find(payerName);
        if (payer == null)
        {
            return 0;
        }

        PayerCallResult result = await _payerClient.GetFhirAsync(payer, "Task?owner=" + Uri.EscapeDataString(organizationNpi), ct);
        if (!result.IsSuccess())
        {
            _logger.LogWarning("Task search at {Payer} failed: HTTP {Status}", payerName, result.StatusCode);
            return 0;
        }

        Bundle? bundle = FhirJson.Read<Bundle>(result.Body);
        if (bundle == null)
        {
            return 0;
        }
        int matched = 0;
        foreach (JsonObject raw in bundle.ResourcesOfType("Task"))
        {
            if (await ProcessTaskAsync(payer, raw, ct))
            {
                matched++;
            }
        }
        return matched;
    }

    /// <summary>A Task (or a Bundle of Tasks) pushed by the payer.</summary>
    public async Task<NotificationResult> ReceivePushedTasksAsync(string payerName, string? authorization, JsonObject? body, CancellationToken ct)
    {
        PayerSettings payer = _payers.GetRequired(payerName, 404);
        if (!SecretCompare.BearerMatches(payer.NotificationSecret, authorization))
        {
            throw new ApiException(401, "Invalid credentials.");
        }

        List<JsonObject> tasks = new List<JsonObject>();
        string? type = FhirJson.ResourceType(body);
        if (type == "Task" && body != null)
        {
            tasks.Add(body);
        }
        else if (type == "Bundle")
        {
            Bundle? bundle = FhirJson.Read<Bundle>(body);
            if (bundle != null)
            {
                tasks.AddRange(bundle.ResourcesOfType("Task"));
            }
        }
        else
        {
            throw new ApiException(400, "A Task or a Bundle of Tasks is expected.");
        }

        int matched = 0;
        foreach (JsonObject raw in tasks)
        {
            if (await ProcessTaskAsync(payer, raw, ct))
            {
                matched++;
            }
        }
        if (matched == 0)
        {
            throw new ApiException(422, "The Task does not match a prior authorization request sent by this gateway.");
        }

        NotificationResult outcome = new NotificationResult();
        outcome.Status = "accepted";
        outcome.Count = matched;
        return outcome;
    }

    /// <summary>Stores the Task, updates the request's informationRequests and tells the EHR when something changed.</summary>
    private async Task<bool> ProcessTaskAsync(PayerSettings payer, JsonObject raw, CancellationToken ct)
    {
        FhirTask? task = FhirJson.Read<FhirTask>(raw);
        if (task == null || task.Id == null)
        {
            return false;
        }
        string? requestId = TaskReader.RequestId(task);
        Identifier? tracking = TaskReader.TrackingIdentifier(task);
        PriorAuthRecord? record = requestId == null ? null : await _requests.GetAsync(requestId, ct);
        if (record == null || tracking == null || tracking.Value == null
            || !string.Equals(record.PayerName, payer.PayerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;   // not one of ours
        }

        CdexTaskRecord taskRecord = new CdexTaskRecord();
        taskRecord.PayerName = payer.PayerName;
        taskRecord.TaskId = task.Id;
        taskRecord.RequestId = record.RequestId;
        taskRecord.TrackingId = tracking.Value;
        taskRecord.TrackingSystem = tracking.System;
        taskRecord.Status = task.Status;
        taskRecord.DueDate = task.Restriction != null && task.Restriction.Period != null ? task.Restriction.Period.End : null;
        taskRecord.TaskJson = raw.ToJsonString();
        await _tasks.SaveAsync(taskRecord, ct);

        // One information request per tracking id on the decision
        InformationRequest info = TaskReader.ToInformationRequest(task);
        List<InformationRequest> list = record.Decision.InformationRequests;
        int index = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].TrackingId == info.TrackingId)
            {
                index = i;
                break;
            }
        }
        bool changed = index < 0 || list[index].Status != info.Status;
        if (index < 0)
        {
            list.Add(info);
        }
        else
        {
            list[index] = info;
        }

        if (changed)
        {
            _logger.LogInformation("Information request {TrackingId} for {RequestId}: {Status}", info.TrackingId, record.RequestId, info.Status);
            await _pas.WriteBackAsync(record, ct);   // saves the record and tells the EHR
        }
        else
        {
            await _requests.SaveAsync(record, ct);
        }
        return true;
    }

    // ================================================================== 3. $submit-attachment

    /// <summary>Sends additional documents and / or completed forms to the payer for a pended prior auth request.</summary>
    public async Task<AttachmentResult> SubmitAttachmentsAsync(string requestId, AttachmentSubmitRequest request, CancellationToken ct)
    {
        PriorAuthRecord record = await GetRecordAsync(requestId, ct);
        if (TextHelper.Distinct(request.DocumentIds).Count == 0 && TextHelper.Distinct(request.QuestionnaireResponseIds).Count == 0)
        {
            throw new ApiException(400, "Send at least one documentId or questionnaireResponseId.");
        }
        if (string.IsNullOrEmpty(record.MemberId))
        {
            throw new ApiException(422, "The request has no member id (Coverage.subscriberId).");
        }
        PayerSettings? payer = _payers.Find(record.PayerName);
        if (payer == null)
        {
            throw new ApiException(422, "Payer " + record.PayerName + " is no longer configured.");
        }

        Identifier trackingId = await ChooseTrackingIdAsync(record, request.TrackingId, ct);
        Parameters parameters = BuildHeader(record, payer, trackingId);

        // attachments: documents (file embedded) and forms
        int count = 0;
        foreach (string documentId in TextHelper.Distinct(request.DocumentIds))
        {
            JsonObject? document = await _fhir.ReadDocumentWithContentAsync(documentId, ct);
            if (document == null)
            {
                throw new ApiException(404, "Document " + documentId + " was not found at the Provider FHIR API.");
            }
            Coding? loinc = null;
            DocumentReference? typed = FhirJson.Read<DocumentReference>(document);
            if (typed != null && typed.Type != null)
            {
                loinc = typed.Type.CodingWithSystem(CodeSystems.Loinc);
            }
            parameters.Parameter.Add(AttachmentPart(document, loinc, request.LineNumbers));
            count++;
        }
        foreach (string formId in TextHelper.Distinct(request.QuestionnaireResponseIds))
        {
            JsonObject? form = await _fhir.ReadAsync("QuestionnaireResponse", formId, ct);
            if (form == null)
            {
                throw new ApiException(404, "QuestionnaireResponse " + formId + " was not found at the Provider FHIR API.");
            }
            parameters.Parameter.Add(AttachmentPart(form, new Coding(CodeSystems.Loinc, CdexCodes.PayerQuestionnaireLoinc, null), request.LineNumbers));
            count++;
        }

        ParametersParameter final = new ParametersParameter();
        final.Name = CdexCodes.Final;
        final.ValueBoolean = request.Final;
        parameters.Parameter.Add(final);

        // send
        PayerCallResult result = await _payerClient.PostFhirAsync(payer, CdexCodes.SubmitAttachment, parameters, ct);
        AttachmentResult summary = new AttachmentResult();
        summary.RequestId = requestId;
        summary.TrackingId = trackingId.Value ?? string.Empty;
        summary.AttachmentCount = count;
        summary.Status = result.IsSuccess() ? "sent" : "rejected";
        if (result.Outcome != null)
        {
            summary.Messages = result.Outcome.Messages();
        }
        if (!result.IsSuccess())
        {
            int status = result.StatusCode >= 400 && result.StatusCode < 500 ? 422 : 502;
            summary.Messages = result.Messages();
            throw new ApiException(status, string.Join("; ", summary.Messages), summary);
        }

        // Mark the information request answered, then ask the payer for the new decision
        foreach (InformationRequest info in record.Decision.InformationRequests)
        {
            if (info.TrackingId == summary.TrackingId)
            {
                info.Status = request.Final ? "completed" : "in-progress";
            }
        }
        await _requests.SaveAsync(record, ct);

        if (request.Final)
        {
            try
            {
                summary.Decision = await _pas.InquireAsync(requestId, true, ct);
            }
            catch (ApiException ex)
            {
                _logger.LogWarning(ex, "Inquiry after attachments failed");
            }
            catch (PayerException ex)
            {
                _logger.LogWarning(ex, "Inquiry after attachments failed");
            }
        }
        return summary;
    }

    // ================================================================== helpers

    /// <summary>
    /// Tracking id to send: the one given, else the latest open information request, else our own request id
    /// (an unsolicited attachment - the payer matches it to the Claim identifier).
    /// </summary>
    private async Task<Identifier> ChooseTrackingIdAsync(PriorAuthRecord record, string? requested, CancellationToken ct)
    {
        string? trackingId = requested;
        if (string.IsNullOrWhiteSpace(trackingId))
        {
            foreach (InformationRequest info in record.Decision.InformationRequests)
            {
                if (info.Status != "completed" && info.Status != "cancelled")
                {
                    trackingId = info.TrackingId;   // the last open one wins
                }
            }
        }
        if (string.IsNullOrWhiteSpace(trackingId) || trackingId == record.RequestId)
        {
            return new Identifier(_workflow.ClaimIdentifierSystem, record.RequestId);
        }

        List<CdexTaskRecord> tasks = await _tasks.GetForRequestAsync(record.RequestId, ct);
        foreach (CdexTaskRecord task in tasks)
        {
            if (task.TrackingId == trackingId)
            {
                return new Identifier(task.TrackingSystem, task.TrackingId);
            }
        }
        throw new ApiException(400, "Tracking id " + trackingId + " is not an information request of " + record.RequestId + ".");
    }

    /// <summary>The $submit-attachment header parameters: TrackingId, AttachTo, PayerId, OrganizationId, ProviderId, MemberId.</summary>
    private static Parameters BuildHeader(PriorAuthRecord record, PayerSettings payer, Identifier trackingId)
    {
        Parameters parameters = new Parameters();
        parameters.Meta = new Meta();
        parameters.Meta.Profile.Add(CdexCodes.ParametersProfile);

        parameters.Parameter.Add(IdentifierParameter(CdexCodes.TrackingId, trackingId));

        ParametersParameter attachTo = new ParametersParameter();
        attachTo.Name = CdexCodes.AttachTo;
        attachTo.ValueCode = CdexCodes.PreAuthorization;
        parameters.Parameter.Add(attachTo);

        string payerId = string.IsNullOrEmpty(payer.PayerIdentifier) ? payer.PayerName : payer.PayerIdentifier;
        parameters.Parameter.Add(IdentifierParameter(CdexCodes.PayerId, new Identifier(payer.PayerIdSystem, payerId)));
        if (record.OrganizationNpi != null)
        {
            parameters.Parameter.Add(IdentifierParameter(CdexCodes.OrganizationId, new Identifier(CodeSystems.Npi, record.OrganizationNpi)));
        }
        if (record.PractitionerNpi != null)
        {
            parameters.Parameter.Add(IdentifierParameter(CdexCodes.ProviderId, new Identifier(CodeSystems.Npi, record.PractitionerNpi)));
        }
        parameters.Parameter.Add(IdentifierParameter(CdexCodes.MemberId, new Identifier(payer.MemberIdSystem, record.MemberId)));
        return parameters;
    }

    /// <summary>A parameter holding an Identifier.</summary>
    private static ParametersParameter IdentifierParameter(string name, Identifier value)
    {
        ParametersParameter parameter = new ParametersParameter();
        parameter.Name = name;
        parameter.ValueIdentifier = value;
        return parameter;
    }

    /// <summary>One "Attachment" parameter: line numbers, LOINC code and the resource itself.</summary>
    private static ParametersParameter AttachmentPart(JsonObject content, Coding? loinc, List<int> lineNumbers)
    {
        ParametersParameter part = new ParametersParameter();
        part.Name = CdexCodes.Attachment;

        List<int> added = new List<int>();
        foreach (int line in lineNumbers)
        {
            if (added.Contains(line))
            {
                continue;
            }
            added.Add(line);
            ParametersParameter lineItem = new ParametersParameter();
            lineItem.Name = CdexCodes.LineItem;
            lineItem.ValueString = line.ToString();
            part.Part.Add(lineItem);
        }
        if (loinc != null && loinc.Code != null)
        {
            ParametersParameter code = new ParametersParameter();
            code.Name = CdexCodes.Code;
            code.ValueCodeableConcept = new CodeableConcept(CodeSystems.Loinc, loinc.Code, loinc.Display);
            part.Part.Add(code);
        }
        ParametersParameter resource = new ParametersParameter();
        resource.Name = CdexCodes.Content;
        resource.Resource = FhirJson.Copy(content);
        part.Part.Add(resource);
        return part;
    }

    /// <summary>The stored request, or 404.</summary>
    private async Task<PriorAuthRecord> GetRecordAsync(string requestId, CancellationToken ct)
    {
        PriorAuthRecord? record = await _requests.GetAsync(requestId, ct);
        if (record == null)
        {
            throw new ApiException(404, "Request " + requestId + " was not found.");
        }
        return record;
    }
}
