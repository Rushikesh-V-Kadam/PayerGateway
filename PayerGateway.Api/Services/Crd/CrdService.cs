using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Clients;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.CdsHooks;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Repositories;
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Services.Crd;

/// <summary>
/// STEP 1 - CRD (Coverage Requirements Discovery), order-sign only. Called when the clinician signs orders.
///
///   1. find the payer by payerName (appsettings "Payers")
///   2. read the patient, the orders and the coverage from the Provider FHIR API
///   3. get the payer's CDS services (discovery, cached 12 h) -> the order-sign service id + prefetch templates
///   4. call POST {payer}/cds-services/{id} with context, prefetch and a fhirAuthorization token
///   5. turn the answer into one CoverageDecision per order (pa-needed, doc-needed, questionnaires, callDtr, callPas)
///   6. save, write each decision back to the EHR, and return them
/// </summary>
public class CrdService
{
    private readonly FhirApiClient _fhir;
    private readonly EhrClient _ehr;
    private readonly PayerRegistry _payers;
    private readonly PayerClient _payerClient;
    private readonly CdsHookBuilder _builder;
    private readonly ICrdRepository _crdRepository;
    private readonly IDtrLinkRepository _linkRepository;
    private readonly WorkflowSettings _workflow;
    private readonly ILogger<CrdService> _logger;

    /// <summary>Created by dependency injection.</summary>
    public CrdService(FhirApiClient fhir, EhrClient ehr, PayerRegistry payers, PayerClient payerClient, CdsHookBuilder builder,
        ICrdRepository crdRepository, IDtrLinkRepository linkRepository, IOptions<WorkflowSettings> workflow, ILogger<CrdService> logger)
    {
        _fhir = fhir;
        _ehr = ehr;
        _payers = payers;
        _payerClient = payerClient;
        _builder = builder;
        _crdRepository = crdRepository;
        _linkRepository = linkRepository;
        _workflow = workflow.Value;
        _logger = logger;
    }

    // ================================================================== entry point

    /// <summary>Runs order-sign for one EHR event.</summary>
    public async Task<OrderSignResponse> OrderSignAsync(OrderSignRequest request, CancellationToken ct)
    {
        // ---- check the input
        List<string> problems = new List<string>();
        if (string.IsNullOrWhiteSpace(request.EventId)) problems.Add("eventId is required.");
        if (string.IsNullOrWhiteSpace(request.PayerName)) problems.Add("payerName is required.");
        if (string.IsNullOrWhiteSpace(request.PatientId)) problems.Add("patientId is required.");
        if (TextHelper.Distinct(request.OrderIds).Count == 0) problems.Add("orderIds must contain at least one order id.");
        if (problems.Count > 0)
        {
            throw new ApiException(400, "The request is not valid.", problems);
        }

        // ---- 1. the payer
        PayerSettings payer = _payers.GetRequired(request.PayerName, 400);

        // Same eventId again -> same answer, no second payer call (the EHR may retry)
        CrdEventRecord? previous = await _crdRepository.GetEventAsync(request.EventId, ct);
        if (previous != null)
        {
            OrderSignResponse? stored = JsonSerializer.Deserialize<OrderSignResponse>(previous.ResponseJson, ApiJson.Options);
            if (stored != null)
            {
                return stored;
            }
        }

        // ---- 2. data from the Provider FHIR API
        JsonObject? patient = await _fhir.ReadAsync("Patient", request.PatientId, ct);
        if (patient == null)
        {
            throw new ApiException(404, "Patient " + request.PatientId + " was not found at the Provider FHIR API.");
        }

        OrderSignResponse response = new OrderSignResponse();
        response.EventId = request.EventId;
        response.PayerName = payer.PayerName;
        response.PayerCall.PayerName = payer.PayerName;

        List<JsonObject> orders = await ReadOrdersAsync(request, response, ct);
        if (orders.Count == 0)
        {
            throw new ApiException(404, "None of the orders in orderIds were found at the Provider FHIR API.");
        }

        List<Coverage> coverages = await _fhir.GetActiveCoveragesAsync(request.PatientId, ct);
        Coverage? coverage = OrderHelper.ChooseCoverage(coverages, request.CoverageId, orders);

        // ---- 3-5. ask the payer (or explain why it could not be asked)
        if (coverage == null)
        {
            response.PayerCall.Outcome = "skipped";
            response.PayerCall.Error = "No active insurance on file.";
            AddUnknownDecisions(response, orders, payer, null, "No active insurance on file for this patient. Coverage requirements could not be checked.");
        }
        else
        {
            await CallPayerAsync(request, payer, coverage, orders, response, ct);
        }

        // ---- 6. save + write back
        foreach (CoverageDecision decision in response.Decisions)
        {
            CoverageDecisionReader.SetNextSteps(decision);

            CoverageDecisionRecord record = new CoverageDecisionRecord();
            record.OrderId = decision.OrderId;
            record.EventId = request.EventId;
            record.PayerName = decision.PayerName;
            record.PatientId = request.PatientId;
            record.CoverageAssertionId = decision.CoverageAssertionId;
            record.Covered = decision.Covered;
            record.PriorAuthNeeded = decision.PriorAuthNeeded;
            record.DocumentationNeeded = decision.DocumentationNeeded;
            record.DecisionJson = ApiJson.Write(decision);
            await _crdRepository.SaveDecisionAsync(record, ct);

            if (request.WriteBack)
            {
                await WriteBackAsync(decision, ct);
            }
        }

        CrdEventRecord eventRecord = new CrdEventRecord();
        eventRecord.EventId = request.EventId;
        eventRecord.PatientId = request.PatientId;
        eventRecord.PayerName = payer.PayerName;
        eventRecord.ResponseJson = ApiJson.Write(response);
        await _crdRepository.SaveEventAsync(eventRecord, ct);

        _logger.LogInformation("order-sign event {EventId} at {Payer}: {Count} decision(s)", request.EventId, payer.PayerName, response.Decisions.Count);
        return response;
    }

    /// <summary>Latest stored decision of an order (no payer call).</summary>
    public async Task<CoverageDecision> GetDecisionAsync(string orderId, CancellationToken ct)
    {
        CoverageDecisionRecord? record = await _crdRepository.GetDecisionAsync(orderId, ct);
        if (record == null)
        {
            throw new ApiException(404, "No coverage decision for order " + orderId + ". Call POST /api/v1/crd/order-sign first.");
        }
        CoverageDecision? decision = JsonSerializer.Deserialize<CoverageDecision>(record.DecisionJson, ApiJson.Options);
        if (decision == null)
        {
            throw new ApiException(500, "The stored decision of order " + orderId + " could not be read.");
        }
        return decision;
    }

    // ================================================================== 2. orders

    /// <summary>Reads the orders; a missing order becomes a warning, an order of another patient an error (400).</summary>
    private async Task<List<JsonObject>> ReadOrdersAsync(OrderSignRequest request, OrderSignResponse response, CancellationToken ct)
    {
        List<string> orderIds = TextHelper.Distinct(request.OrderIds);
        List<JsonObject> found = await _fhir.ReadOrdersAsync(orderIds, ct);
        List<JsonObject> orders = new List<JsonObject>();

        foreach (string orderId in orderIds)
        {
            JsonObject? raw = OrderHelper.FindOrder(found, orderId);
            if (raw == null)
            {
                EhrMessage message = new EhrMessage();
                message.Severity = "warning";
                message.Summary = "Order " + orderId + " was not found.";
                response.Messages.Add(message);
                continue;
            }
            if (OrderHelper.ReadOrder(raw).GetPatientId() != request.PatientId)
            {
                throw new ApiException(400, "Order " + orderId + " does not belong to patient " + request.PatientId + ".");
            }
            orders.Add(raw);
        }
        return orders;
    }

    // ================================================================== 3-5. the payer call

    /// <summary>Calls the payer's order-sign service and adds the decisions / messages to the response.</summary>
    private async Task CallPayerAsync(OrderSignRequest request, PayerSettings payer, Coverage coverage,
        List<JsonObject> orders, OrderSignResponse response, CancellationToken ct)
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            // ---- 3. discovery: which CDS service handles order-sign (cached 12 h)
            CdsDiscoveryResponse? discovery = _payers.GetCachedDiscovery(payer.PayerName);
            if (discovery == null)
            {
                discovery = await _payerClient.DiscoverAsync(payer, ct);
                _payers.CacheDiscovery(payer.PayerName, discovery);
            }

            CdsServiceDefinition? service = FindOrderSignService(discovery);
            if (service == null)
            {
                response.PayerCall.Outcome = "skipped";
                response.PayerCall.Error = payer.GetDisplayName() + " has no CDS service for order-sign.";
                AddUnknownDecisions(response, orders, payer, coverage.Id, response.PayerCall.Error);
                return;
            }
            response.PayerCall.ServiceId = service.Id;

            // ---- 4. the hook call
            string? user = request.UserPractitionerId;
            if (string.IsNullOrWhiteSpace(user))
            {
                user = OrderHelper.RequesterOfOrders(orders);
            }

            CdsHookRequest hookRequest = new CdsHookRequest();
            hookRequest.Hook = CrdCodes.OrderSignHook;
            hookRequest.FhirServer = _fhir.GetBaseUrl();
            hookRequest.FhirAuthorization = await TryGetFhirAuthorizationAsync(payer, request.PatientId, ct);
            hookRequest.Context = _builder.BuildContext(request, orders, user);
            if (service.Prefetch != null && service.Prefetch.Count > 0)
            {
                hookRequest.Prefetch = await _builder.BuildPrefetchAsync(service.Prefetch, request, orders, user, ct);
            }

            CdsHookResponse hookResponse = await _payerClient.CallHookAsync(payer, service.Id, hookRequest, ct);

            // ---- 5a. coverage information -> one decision per order
            Dictionary<string, CoverageDecision> byReference = CoverageDecisionReader.Read(hookResponse);
            foreach (JsonObject raw in orders)
            {
                OrderResource order = OrderHelper.ReadOrder(raw);
                CoverageDecision? decision;
                if (byReference.TryGetValue(order.GetReference(), out decision))
                {
                    decision.OrderId = order.Id ?? string.Empty;
                    decision.PayerName = payer.PayerName;
                    if (decision.CoverageId == null)
                    {
                        decision.CoverageId = coverage.Id;
                    }
                    response.Decisions.Add(decision);
                }
                else
                {
                    response.Decisions.Add(UnknownDecision(order.Id ?? string.Empty, payer, coverage.Id,
                        payer.GetDisplayName() + " returned no coverage information for this order."));
                }
            }

            // ---- 5b. cards -> messages (on the order they mention, else on the response)
            foreach (CdsCard card in hookResponse.Cards)
            {
                string? orderId = OrderMentionedByCard(card, orders);
                EhrMessage message = await ToMessageAsync(card, payer, request, orders, orderId, coverage.Id, ct);
                CoverageDecision? owner = orderId == null ? null : FindDecision(response, orderId);
                if (owner != null)
                {
                    owner.Messages.Add(message);
                }
                else
                {
                    response.Messages.Add(message);
                }
            }
        }
        catch (PayerException ex)
        {
            _logger.LogWarning(ex, "order-sign call to {Payer} failed", payer.PayerName);
            response.PayerCall.Outcome = ex.StatusCode == 504 ? "timeout" : "error";
            response.PayerCall.Error = ex.Message;
            AddUnknownDecisions(response, orders, payer, coverage.Id,
                payer.GetDisplayName() + " did not answer the coverage check. Continue; requirements are checked again at prior authorization.");
        }
        finally
        {
            response.PayerCall.DurationMs = watch.ElapsedMilliseconds;
        }
    }

    /// <summary>The payer's CDS service for the order-sign hook, or null.</summary>
    private static CdsServiceDefinition? FindOrderSignService(CdsDiscoveryResponse discovery)
    {
        foreach (CdsServiceDefinition service in discovery.Services)
        {
            if (service.Hook == CrdCodes.OrderSignHook)
            {
                return service;
            }
        }
        return null;
    }

    /// <summary>fhirAuthorization lets the payer read more data during CRD. Optional: the hook is sent without it on failure.</summary>
    private async Task<JsonObject?> TryGetFhirAuthorizationAsync(PayerSettings payer, string patientId, CancellationToken ct)
    {
        try
        {
            return await _fhir.IssueAccessTokenAsync(payer.PayerName, patientId, _workflow.FhirAuthorizationMinutes, ct);
        }
        catch (FhirApiException ex)
        {
            _logger.LogWarning(ex, "No fhirAuthorization for {Payer}; hook sent without it", payer.PayerName);
            return null;
        }
    }

    // ================================================================== cards

    /// <summary>The order a card is about: the only order, or the one whose "Type/id" appears in the card. Null when unclear.</summary>
    private static string? OrderMentionedByCard(CdsCard card, List<JsonObject> orders)
    {
        if (orders.Count == 1)
        {
            return FhirJson.Id(orders[0]);
        }

        string text = card.Summary + " " + card.Detail;
        foreach (CdsLink link in card.Links)
        {
            text = text + " " + link.AppContext;
        }
        foreach (JsonObject raw in orders)
        {
            OrderResource order = OrderHelper.ReadOrder(raw);
            if (text.Contains(order.GetReference(), StringComparison.Ordinal))
            {
                return order.Id;
            }
        }
        return null;
    }

    /// <summary>
    /// Card -> EHR message. A SMART link (a payer DTR app) cannot be opened by the EHR directly, so the launch details are
    /// saved and the EHR gets a plain gateway URL: /api/v1/dtr/launch/{linkId}.
    /// </summary>
    private async Task<EhrMessage> ToMessageAsync(CdsCard card, PayerSettings payer, OrderSignRequest request, List<JsonObject> orders,
        string? orderId, string? coverageId, CancellationToken ct)
    {
        EhrMessage message = new EhrMessage();
        message.Severity = card.Indicator;
        message.Summary = card.Summary;
        message.Detail = card.Detail;

        CdsLink? link = null;
        foreach (CdsLink candidate in card.Links)
        {
            if (candidate.Type == CrdCodes.LinkTypeSmart)
            {
                link = candidate;
                break;
            }
        }
        if (link == null && card.Links.Count > 0)
        {
            link = card.Links[0];
        }
        if (link == null)
        {
            return message;
        }

        message.Link = new EhrMessageLink();
        message.Link.Label = link.Label;
        if (link.Type != CrdCodes.LinkTypeSmart)
        {
            message.Link.Url = link.Url;
            message.Link.Type = CrdCodes.LinkTypeAbsolute;
            return message;
        }

        // SMART link: remember the launch context, give the EHR a gateway URL
        DtrLinkRecord dtrLink = new DtrLinkRecord();
        dtrLink.PayerName = payer.PayerName;
        dtrLink.PatientId = request.PatientId;
        dtrLink.EncounterId = request.EncounterId;
        dtrLink.UserPractitionerId = request.UserPractitionerId;
        foreach (JsonObject raw in orders)
        {
            OrderResource order = OrderHelper.ReadOrder(raw);
            if (orderId == null || order.Id == orderId)
            {
                dtrLink.FhirContext.Add(order.GetReference());
            }
        }
        if (coverageId != null)
        {
            dtrLink.FhirContext.Add("Coverage/" + coverageId);
        }
        dtrLink.AppContext = link.AppContext;
        dtrLink.AppUrl = string.IsNullOrEmpty(_workflow.DtrAppUrl) ? link.Url : _workflow.DtrAppUrl;
        dtrLink.ExpiresAt = DateTime.UtcNow.AddHours(_workflow.DtrLinkHours);
        await _linkRepository.SaveAsync(dtrLink, ct);

        message.Link.Url = _workflow.GetGatewayBaseUrl() + "/api/v1/dtr/launch/" + dtrLink.LinkId;
        message.Link.Type = CrdCodes.LinkTypeSmart;
        return message;
    }

    // ================================================================== helpers

    /// <summary>The decision of an order in the response, or null.</summary>
    private static CoverageDecision? FindDecision(OrderSignResponse response, string orderId)
    {
        foreach (CoverageDecision decision in response.Decisions)
        {
            if (decision.OrderId == orderId)
            {
                return decision;
            }
        }
        return null;
    }

    /// <summary>Adds an "unknown" decision for every order (the payer could not be asked).</summary>
    private static void AddUnknownDecisions(OrderSignResponse response, List<JsonObject> orders, PayerSettings payer, string? coverageId, string text)
    {
        foreach (JsonObject raw in orders)
        {
            string orderId = FhirJson.Id(raw) ?? string.Empty;
            response.Decisions.Add(UnknownDecision(orderId, payer, coverageId, text));
        }
    }

    /// <summary>Decision used when the payer could not be asked (no insurance, no service, timeout).</summary>
    private static CoverageDecision UnknownDecision(string orderId, PayerSettings payer, string? coverageId, string text)
    {
        CoverageDecision decision = new CoverageDecision();
        decision.OrderId = orderId;
        decision.PayerName = payer.PayerName;
        decision.CoverageId = coverageId;
        decision.Covered = CrdCodes.Unknown;
        decision.DecidedAt = FhirDate.Now();

        EhrMessage message = new EhrMessage();
        message.Severity = "warning";
        message.Summary = TextHelper.Shorten(text, 140);
        message.Detail = text;
        decision.Messages.Add(message);
        return decision;
    }

    /// <summary>Sends the decision to the EHR. A failure is only logged: the EHR can read it with GET /api/v1/crd/decisions/{orderId}.</summary>
    private async Task WriteBackAsync(CoverageDecision decision, CancellationToken ct)
    {
        try
        {
            await _ehr.PutCoverageDecisionAsync(decision, ct);
        }
        catch (EhrException ex)
        {
            _logger.LogWarning(ex, "Coverage decision write-back for order {OrderId} failed", decision.OrderId);
        }
    }
}
