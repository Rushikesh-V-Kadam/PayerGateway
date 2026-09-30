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
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Services.Pas;

/// <summary>
/// STEP 3 - PAS (Prior Authorization Support). Sends prior authorization requests to the payer named by the EHR
/// and tracks the decision.
///
///   submit  : EHR request -> FHIR data from the Provider FHIR API (+ completed DTR forms, documents) -> PAS Bundle
///             -> POST {payer}/Claim/$submit -> ClaimResponse -> PriorAuthDecision -> saved, written back, returned
///   update  : same, with the Claim Update profile; removed orders are sent as cancelled lines
///   cancel  : every line sent as cancelled
///   inquire : POST {payer}/Claim/$inquire to get the latest decision of a pended request
///   notification : the payer says something changed -> inquire the payer's open requests
/// A pended request can also get a payer information request (Task) -> STEP 4, CdexService.
/// </summary>
public class PasService
{
    private readonly FhirApiClient _fhir;
    private readonly EhrClient _ehr;
    private readonly PayerRegistry _payers;
    private readonly PayerClient _payerClient;
    private readonly PasBundleBuilder _builder;
    private readonly PasSubscriptionService _subscriptions;
    private readonly IPriorAuthRepository _repository;
    private readonly WorkflowSettings _workflow;
    private readonly ILogger<PasService> _logger;

    /// <summary>Created by dependency injection.</summary>
    public PasService(FhirApiClient fhir, EhrClient ehr, PayerRegistry payers, PayerClient payerClient, PasBundleBuilder builder,
        PasSubscriptionService subscriptions, IPriorAuthRepository repository, IOptions<WorkflowSettings> workflow, ILogger<PasService> logger)
    {
        _fhir = fhir;
        _ehr = ehr;
        _payers = payers;
        _payerClient = payerClient;
        _builder = builder;
        _subscriptions = subscriptions;
        _repository = repository;
        _workflow = workflow.Value;
        _logger = logger;
    }

    // ================================================================== submit / update

    /// <summary>
    /// Submits a new request (isUpdate = false) or changes an existing one (isUpdate = true).
    /// Nothing is saved unless the payer accepts the request.
    /// </summary>
    public async Task<PriorAuthDecision> SubmitAsync(PriorAuthRequest request, bool isUpdate, CancellationToken ct)
    {
        // ---- 1. check the input
        CheckRequest(request);
        PayerSettings payer = _payers.GetRequired(request.PayerName, 400);
        List<string> orderIds = TextHelper.Distinct(request.OrderIds);

        PriorAuthRecord? existing = await _repository.GetAsync(request.RequestId, ct);
        if (!isUpdate && existing != null && existing.Status != DecisionStatus.Cancelled)
        {
            throw new ApiException(409, "Request " + request.RequestId + " was already submitted. Use PUT /api/v1/pas/requests/" + request.RequestId + " to change it.");
        }
        if (isUpdate && existing == null)
        {
            throw new ApiException(404, "Request " + request.RequestId + " was not found.");
        }
        if (isUpdate && existing != null)
        {
            if (existing.PatientId != request.PatientId)
            {
                throw new ApiException(400, "patientId cannot change on an update.");
            }
            if (!string.Equals(existing.PayerName, payer.PayerName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ApiException(400, "The payer of an existing request cannot change. Cancel it and submit a new request.");
            }
            if (existing.Status == DecisionStatus.Cancelled)
            {
                throw new ApiException(409, "Request " + request.RequestId + " is cancelled. Submit it again with POST /api/v1/pas/requests.");
            }
        }

        // ---- 2. data from the Provider FHIR API
        JsonObject? patient = await _fhir.ReadAsync("Patient", request.PatientId, ct);
        if (patient == null)
        {
            throw new ApiException(404, "Patient " + request.PatientId + " was not found at the Provider FHIR API.");
        }

        List<JsonObject> orders = await _fhir.ReadOrdersAsync(orderIds, ct);
        OrderHelper.CheckOrders(orderIds, orders, request.PatientId);

        string? coverageId = request.CoverageId;
        if (string.IsNullOrWhiteSpace(coverageId) && existing != null)
        {
            coverageId = existing.CoverageId;
        }
        List<Coverage> coverages = await _fhir.GetActiveCoveragesAsync(request.PatientId, ct);
        Coverage? coverage = OrderHelper.ChooseCoverage(coverages, coverageId, orders);
        if (coverage == null || coverage.Id == null)
        {
            throw new ApiException(422, "Patient " + request.PatientId + " has no active insurance for this request.");
        }

        JsonObject? organization = await _fhir.ReadAsync("Organization", request.RequestingOrganizationId, ct);
        if (organization == null)
        {
            throw new ApiException(404, "Organization " + request.RequestingOrganizationId + " was not found at the Provider FHIR API.");
        }
        string? organizationNpi = ReadNpi(organization);
        if (organizationNpi == null)
        {
            throw new ApiException(422, "Organization " + request.RequestingOrganizationId + " has no NPI (PAS requires it).");
        }

        string? practitionerId = request.RequestingPractitionerId;
        if (string.IsNullOrWhiteSpace(practitionerId))
        {
            practitionerId = OrderHelper.RequesterOfOrders(orders);
        }
        JsonObject? practitioner = null;
        if (!string.IsNullOrWhiteSpace(practitionerId))
        {
            practitioner = await _fhir.ReadAsync("Practitioner", practitionerId, ct);
        }

        JsonObject? referring = null;
        if (!string.IsNullOrWhiteSpace(request.ReferringPractitionerId))
        {
            referring = await _fhir.ReadAsync("Practitioner", request.ReferringPractitionerId, ct);
            if (referring == null)
            {
                throw new ApiException(404, "Referring practitioner " + request.ReferringPractitionerId + " was not found at the Provider FHIR API.");
            }
        }

        List<JsonObject> forms = await ReadFormsAsync(request, orderIds, ct);
        List<JsonObject> documents = await ReadDocumentsAsync(request.DocumentIds, ct);

        // ---- 3. orders removed on update become cancelled lines; every order keeps its line number
        List<string> previousOrderIds = existing != null ? existing.OrderIds : new List<string>();
        List<string> removedIds = new List<string>();
        if (isUpdate)
        {
            foreach (string previousId in previousOrderIds)
            {
                if (!orderIds.Contains(previousId))
                {
                    removedIds.Add(previousId);
                }
            }
        }
        List<JsonObject> removedOrders = new List<JsonObject>();
        if (removedIds.Count > 0)
        {
            removedOrders = await _fhir.ReadOrdersAsync(removedIds, ct);
        }

        List<string> allLineOrderIds = new List<string>(orderIds);
        allLineOrderIds.AddRange(removedIds);
        Dictionary<string, int> startNumbers = isUpdate && existing != null ? existing.LineNumbers : new Dictionary<string, int>();
        Dictionary<string, int> lineNumbers = AssignLineNumbers(startNumbers, allLineOrderIds);

        // ---- 4. build and send the PAS Bundle
        int version = (isUpdate && existing != null ? existing.Version : 0) + 1;
        PasBuildInput input = new PasBuildInput();
        input.RequestId = request.RequestId;
        input.Version = version;
        input.Priority = request.Priority;
        input.Payer = payer;
        input.Patient = patient;
        input.Coverage = coverage;
        input.Organization = organization;
        input.Practitioner = practitioner;
        input.Orders = orders;
        input.CancelledOrders = removedOrders;
        input.PreviousOrderIds = previousOrderIds;
        input.LineNumbers = lineNumbers;
        input.Forms = forms;
        input.Documents = documents;
        input.Notes = request.Notes;
        input.RequestType = request.RequestType;
        input.PreviousAuthorizationNumber = request.PreviousAuthorizationNumber;
        input.PreviousServiceStart = request.PreviousServiceStart;
        input.PreviousServiceEnd = request.PreviousServiceEnd;
        input.PreviousTreatments = request.PreviousTreatments;
        input.PatientType = request.PatientType;
        input.ReferringPractitioner = referring;
        Bundle bundle = _builder.BuildRequest(input);

        PayerCallResult result = await _payerClient.PostFhirAsync(payer, PasCodes.Submit, bundle, ct);
        ClaimResponse? claimResponse = PasDecisionReader.FindClaimResponse(result);
        if (!result.IsSuccess() || claimResponse == null)
        {
            // The payer did not accept the request (e.g. validation error). Nothing is saved; the EHR can fix and retry.
            PriorAuthDecision rejected = PasDecisionReader.Rejected(request.RequestId, payer.PayerName, request.PatientId, orderIds, result);
            int status = result.StatusCode >= 400 && result.StatusCode < 500 ? 422 : 502;
            throw new ApiException(status, string.Join("; ", rejected.Errors), rejected);
        }

        // ---- 5. accepted: save the new state
        PriorAuthRecord record;
        if (isUpdate && existing != null)
        {
            record = existing;
        }
        else
        {
            record = new PriorAuthRecord();
            record.RequestId = request.RequestId;
            record.SubmittedAt = DateTime.UtcNow;
        }
        record.PayerName = payer.PayerName;
        record.PatientId = request.PatientId;
        record.CoverageId = coverage.Id;
        record.MemberId = coverage.MemberId();
        record.OrganizationId = request.RequestingOrganizationId;
        record.OrganizationNpi = organizationNpi;
        record.PractitionerId = practitionerId;
        record.PractitionerNpi = practitioner == null ? null : ReadNpi(practitioner);
        record.Priority = request.Priority;
        record.Version = version;
        record.OrderIds = orderIds;
        record.LineNumbers = lineNumbers;
        await ApplyResponseAsync(record, bundle, result, claimResponse, ct);

        if (request.WriteBack)
        {
            await WriteBackAsync(record, ct);
        }
        await _subscriptions.EnsureSubscribedAsync(payer, organizationNpi, ct);
        return record.Decision;
    }

    // ================================================================== cancel

    /// <summary>Cancels the whole request: every line is sent again as cancelled (X12 certification type 3).</summary>
    public async Task<PriorAuthDecision> CancelAsync(string requestId, CancelPriorAuthRequest cancel, CancellationToken ct)
    {
        PriorAuthRecord record = await GetRecordAsync(requestId, ct);
        if (record.Status == DecisionStatus.Cancelled)
        {
            return record.Decision;
        }
        PayerSettings payer = GetPayer(record);

        JsonObject? patient = await _fhir.ReadAsync("Patient", record.PatientId, ct);
        List<Coverage> coverages = await _fhir.GetActiveCoveragesAsync(record.PatientId, ct);
        Coverage? coverage = null;
        foreach (Coverage candidate in coverages)
        {
            if (candidate.Id == record.CoverageId)
            {
                coverage = candidate;
            }
        }
        JsonObject? organization = await _fhir.ReadAsync("Organization", record.OrganizationId, ct);
        List<JsonObject> orders = await _fhir.ReadOrdersAsync(record.OrderIds, ct);
        if (patient == null || coverage == null || organization == null || orders.Count == 0)
        {
            throw new ApiException(422, "The patient, insurance, organization or orders of this request can no longer be read.");
        }

        PasBuildInput input = new PasBuildInput();
        input.RequestId = record.RequestId;
        input.Version = record.Version + 1;
        input.Priority = record.Priority;
        input.Payer = payer;
        input.Patient = patient;
        input.Coverage = coverage;
        input.Organization = organization;
        input.CancelledOrders = orders;
        input.PreviousOrderIds = record.OrderIds;
        input.LineNumbers = record.LineNumbers;
        input.Notes = cancel.Reason;
        Bundle bundle = _builder.BuildRequest(input);

        PayerCallResult result = await _payerClient.PostFhirAsync(payer, PasCodes.Submit, bundle, ct);
        ClaimResponse? claimResponse = PasDecisionReader.FindClaimResponse(result);
        if (!result.IsSuccess() || claimResponse == null)
        {
            PriorAuthDecision rejected = PasDecisionReader.Rejected(record.RequestId, payer.PayerName, record.PatientId, record.OrderIds, result);
            int status = result.StatusCode >= 400 && result.StatusCode < 500 ? 422 : 502;
            throw new ApiException(status, string.Join("; ", rejected.Errors), rejected);
        }

        record.Version = record.Version + 1;
        await ApplyResponseAsync(record, bundle, result, claimResponse, ct);
        if (cancel.WriteBack)
        {
            await WriteBackAsync(record, ct);
        }
        return record.Decision;
    }

    // ================================================================== read / inquire

    /// <summary>Latest stored decision (no payer call).</summary>
    public async Task<PriorAuthDecision> GetAsync(string requestId, CancellationToken ct)
    {
        PriorAuthRecord record = await GetRecordAsync(requestId, ct);
        return record.Decision;
    }

    /// <summary>
    /// Asks the payer for the latest decision (Claim/$inquire). When it changed, it is saved and written back to the EHR.
    /// </summary>
    public async Task<PriorAuthDecision> InquireAsync(string requestId, bool writeBack, CancellationToken ct)
    {
        PriorAuthRecord record = await GetRecordAsync(requestId, ct);
        if (record.LastRequestBundleJson == null)
        {
            return record.Decision;
        }
        PayerSettings payer = GetPayer(record);

        Bundle inquiry = _builder.BuildInquiry(record.LastRequestBundleJson);
        PayerCallResult result = await _payerClient.PostFhirAsync(payer, PasCodes.Inquire, inquiry, ct);
        if (!result.IsSuccess())
        {
            throw new ApiException(502, "Inquiry failed at " + payer.GetDisplayName() + " (HTTP " + result.StatusCode + ").", result.Messages());
        }
        ClaimResponse? claimResponse = PasDecisionReader.FindClaimResponse(result);
        if (claimResponse == null)
        {
            return record.Decision;   // the payer has nothing newer
        }

        PriorAuthDecision before = record.Decision;
        Claim claim = ReadClaim(record.LastRequestBundleJson);
        record.Decision = PasDecisionReader.Read(claimResponse, claim, record, _workflow);
        record.Status = record.Decision.OverallStatus;
        record.AuthorizationNumber = record.Decision.AuthorizationNumber;
        record.LastResponseBundleJson = result.Body == null ? null : result.Body.ToJsonString();
        await _repository.SaveAsync(record, ct);

        if (HasChanged(before, record.Decision))
        {
            _logger.LogInformation("Request {RequestId}: decision is now {Status}", requestId, record.Decision.OverallStatus);
            if (writeBack)
            {
                await WriteBackAsync(record, ct);
            }
        }
        return record.Decision;
    }

    /// <summary>
    /// A PAS Subscription notification from a payer. The payload is id-only (it does not say which request changed),
    /// so every open request of that payer is inquired. Handshake / heartbeat notifications need no action.
    /// </summary>
    public async Task<NotificationResult> HandleNotificationAsync(string payerName, string? authorization, JsonObject? body, CancellationToken ct)
    {
        PayerSettings payer = _payers.GetRequired(payerName, 404);
        if (!SecretCompare.BearerMatches(payer.NotificationSecret, authorization))
        {
            throw new ApiException(401, "Invalid notification credentials.");
        }

        NotificationResult outcome = new NotificationResult();
        string type = NotificationType(body);
        if (type == "handshake" || type == "heartbeat")
        {
            outcome.Status = type;
            return outcome;
        }

        List<PriorAuthRecord> open = await _repository.GetOpenAsync(payer.PayerName, ct);
        foreach (PriorAuthRecord request in open)
        {
            try
            {
                await InquireAsync(request.RequestId, true, ct);
            }
            catch (ApiException ex)
            {
                _logger.LogWarning(ex, "Inquiry of {RequestId} failed", request.RequestId);
            }
            catch (PayerException ex)
            {
                _logger.LogWarning(ex, "Inquiry of {RequestId} failed", request.RequestId);
            }
        }
        outcome.Status = open.Count + " open request(s) checked";
        outcome.Count = open.Count;
        return outcome;
    }

    // ================================================================== EHR write-back

    /// <summary>
    /// Sends the decision to the EHR and records whether it arrived (EhrSyncStatus).
    /// FAILED write-backs are retried by the background poller.
    /// </summary>
    public async Task WriteBackAsync(PriorAuthRecord record, CancellationToken ct)
    {
        try
        {
            await _ehr.PutPriorAuthDecisionAsync(record.Decision, ct);
            record.EhrSyncStatus = PasCodes.EhrSyncSent;
        }
        catch (EhrException ex)
        {
            _logger.LogWarning(ex, "Write-back of {RequestId} failed; will retry", record.RequestId);
            record.EhrSyncStatus = PasCodes.EhrSyncFailed;
        }
        await _repository.SaveAsync(record, ct);
    }

    // ================================================================== helpers

    /// <summary>Required fields and allowed values; all problems are returned together (400).</summary>
    private static void CheckRequest(PriorAuthRequest request)
    {
        // empty header values mean the usual case: a new request for a permanent patient
        if (string.IsNullOrWhiteSpace(request.RequestType)) request.RequestType = PasCodes.RequestTypeNew;
        if (string.IsNullOrWhiteSpace(request.PatientType)) request.PatientType = PasCodes.PatientTypePermanent;

        List<string> problems = new List<string>();
        if (string.IsNullOrWhiteSpace(request.RequestId)) problems.Add("requestId is required.");
        if (string.IsNullOrWhiteSpace(request.PayerName)) problems.Add("payerName is required.");
        if (string.IsNullOrWhiteSpace(request.PatientId)) problems.Add("patientId is required.");
        if (string.IsNullOrWhiteSpace(request.RequestingOrganizationId)) problems.Add("requestingOrganizationId is required.");
        if (TextHelper.Distinct(request.OrderIds).Count == 0) problems.Add("orderIds must contain at least one order id.");
        if (request.Priority != PasCodes.PriorityRoutine && request.Priority != PasCodes.PriorityUrgent) problems.Add("priority must be 'routine' or 'urgent'.");
        if (request.RequestType != PasCodes.RequestTypeNew && request.RequestType != PasCodes.RequestTypeRenewal) problems.Add("requestType must be 'new' or 'renewal'.");
        if (request.RequestType == PasCodes.RequestTypeRenewal && string.IsNullOrWhiteSpace(request.PreviousAuthorizationNumber)) problems.Add("previousAuthorizationNumber is required for a renewal.");
        if (request.PatientType != PasCodes.PatientTypePermanent && request.PatientType != PasCodes.PatientTypeTransient) problems.Add("patientType must be 'permanent' or 'transient'.");
        if (request.PreviousTreatments != null && request.PreviousTreatments.Value < 0) problems.Add("previousTreatments cannot be negative.");
        if (problems.Count > 0)
        {
            throw new ApiException(400, "The request is not valid.", problems);
        }
    }

    /// <summary>Stores the request / response Bundles and the new decision.</summary>
    private async Task ApplyResponseAsync(PriorAuthRecord record, Bundle sent, PayerCallResult result, ClaimResponse claimResponse, CancellationToken ct)
    {
        Claim? claim = FhirJson.Read<Claim>(sent.Entry[0].Resource);
        record.Decision = PasDecisionReader.Read(claimResponse, claim ?? new Claim(), record, _workflow);
        record.Status = record.Decision.OverallStatus;
        record.AuthorizationNumber = record.Decision.AuthorizationNumber;
        record.LastRequestBundleJson = FhirJson.Serialize(sent);
        record.LastResponseBundleJson = result.Body == null ? null : result.Body.ToJsonString();
        await _repository.SaveAsync(record, ct);
        _logger.LogInformation("PAS {RequestId} v{Version} at {Payer}: {Status}", record.RequestId, record.Version, record.PayerName, record.Status);
    }

    /// <summary>
    /// The forms named in the request, plus completed forms already saved for these orders (DTR, via qr-context).
    /// </summary>
    private async Task<List<JsonObject>> ReadFormsAsync(PriorAuthRequest request, List<string> orderIds, CancellationToken ct)
    {
        List<JsonObject> forms = new List<JsonObject>();
        List<string> added = new List<string>();

        foreach (string formId in TextHelper.Distinct(request.QuestionnaireResponseIds))
        {
            JsonObject? form = await _fhir.ReadAsync("QuestionnaireResponse", formId, ct);
            if (form == null)
            {
                throw new ApiException(404, "QuestionnaireResponse " + formId + " was not found at the Provider FHIR API.");
            }
            forms.Add(form);
            added.Add(formId);
        }

        Bundle saved = await _fhir.SearchAsync("QuestionnaireResponse?patient=" + Uri.EscapeDataString(request.PatientId), ct);
        foreach (JsonObject form in saved.ResourcesOfType("QuestionnaireResponse"))
        {
            string? id = FhirJson.Id(form);
            if (id == null || added.Contains(id))
            {
                continue;
            }
            string? status = FhirJson.GetString(form, "status");
            string? orderId = PasBundleBuilder.FormOrderId(form);
            bool finished = status == "completed" || status == "amended";
            if (finished && orderId != null && orderIds.Contains(orderId))
            {
                forms.Add(form);
                added.Add(id);
            }
        }
        return forms;
    }

    /// <summary>Reads each document with its file embedded; a missing document is an error (404).</summary>
    private async Task<List<JsonObject>> ReadDocumentsAsync(List<string> documentIds, CancellationToken ct)
    {
        List<JsonObject> documents = new List<JsonObject>();
        foreach (string documentId in TextHelper.Distinct(documentIds))
        {
            JsonObject? document = await _fhir.ReadDocumentWithContentAsync(documentId, ct);
            if (document == null)
            {
                throw new ApiException(404, "Document " + documentId + " was not found at the Provider FHIR API.");
            }
            documents.Add(document);
        }
        return documents;
    }

    /// <summary>Keeps existing line numbers and gives new orders the next free numbers (1, 2, 3 ...).</summary>
    private static Dictionary<string, int> AssignLineNumbers(Dictionary<string, int> existing, List<string> orderIds)
    {
        Dictionary<string, int> numbers = new Dictionary<string, int>(existing);
        int highest = 0;
        foreach (int number in numbers.Values)
        {
            if (number > highest)
            {
                highest = number;
            }
        }
        foreach (string orderId in orderIds)
        {
            if (!numbers.ContainsKey(orderId))
            {
                highest = highest + 1;
                numbers[orderId] = highest;
            }
        }
        return numbers;
    }

    /// <summary>The NPI of an Organization or Practitioner, or null.</summary>
    private static string? ReadNpi(JsonObject resource)
    {
        IdentifiedResource? typed = FhirJson.Read<IdentifiedResource>(resource);
        return typed == null ? null : typed.IdentifierValue(CodeSystems.Npi);
    }

    /// <summary>The Claim of a stored request Bundle.</summary>
    private static Claim ReadClaim(string bundleJson)
    {
        Bundle? bundle = FhirJson.Deserialize<Bundle>(bundleJson);
        if (bundle == null || bundle.Entry.Count == 0)
        {
            return new Claim();
        }
        Claim? claim = FhirJson.Read<Claim>(bundle.Entry[0].Resource);
        return claim ?? new Claim();
    }

    /// <summary>The "type" of a notification (from its SubscriptionStatus Parameters): handshake | heartbeat | event-notification.</summary>
    private static string NotificationType(JsonObject? body)
    {
        Bundle? bundle = FhirJson.Read<Bundle>(body);
        if (bundle == null)
        {
            return "event-notification";
        }
        foreach (JsonObject raw in bundle.ResourcesOfType("Parameters"))
        {
            Parameters? status = FhirJson.Read<Parameters>(raw);
            if (status == null)
            {
                continue;
            }
            foreach (ParametersParameter parameter in status.Parameter)
            {
                if (parameter.Name == "type" && parameter.ValueCode != null)
                {
                    return parameter.ValueCode;
                }
            }
        }
        return "event-notification";
    }

    /// <summary>The stored request, or 404.</summary>
    private async Task<PriorAuthRecord> GetRecordAsync(string requestId, CancellationToken ct)
    {
        PriorAuthRecord? record = await _repository.GetAsync(requestId, ct);
        if (record == null)
        {
            throw new ApiException(404, "Request " + requestId + " was not found.");
        }
        return record;
    }

    /// <summary>The request's payer, or 422 when it was removed from appsettings.</summary>
    private PayerSettings GetPayer(PriorAuthRecord record)
    {
        PayerSettings? payer = _payers.Find(record.PayerName);
        if (payer == null)
        {
            throw new ApiException(422, "Payer " + record.PayerName + " is no longer configured.");
        }
        return payer;
    }

    /// <summary>True when the EHR should be told again.</summary>
    private static bool HasChanged(PriorAuthDecision before, PriorAuthDecision after)
    {
        if (before.OverallStatus != after.OverallStatus) return true;
        if (before.AuthorizationNumber != after.AuthorizationNumber) return true;
        if (before.Lines.Count != after.Lines.Count) return true;
        for (int i = 0; i < before.Lines.Count; i++)
        {
            if (before.Lines[i].Decision != after.Lines[i].Decision)
            {
                return true;
            }
        }
        return false;
    }
}
