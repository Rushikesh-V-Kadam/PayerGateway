using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Fhir;
using PayerGateway.Api.Repositories;
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Services.Pas;

/// <summary>
/// PAS Subscription (Subscriptions R5 Backport). A pended request can take days; instead of asking the payer again and
/// again, the gateway subscribes once per payer + provider organization. The payer then POSTs a notification to
/// /api/v1/pas/notifications/{payerName} when a ClaimResponse changes, and the gateway runs $inquire.
/// If the payer refuses the subscription, the background poller still runs $inquire periodically.
/// </summary>
public class PasSubscriptionService
{
    private readonly PayerClient _payerClient;
    private readonly IPayerSubscriptionRepository _repository;
    private readonly WorkflowSettings _workflow;
    private readonly ILogger<PasSubscriptionService> _logger;

    /// <summary>Created by dependency injection.</summary>
    public PasSubscriptionService(PayerClient payerClient, IPayerSubscriptionRepository repository,
        IOptions<WorkflowSettings> workflow, ILogger<PasSubscriptionService> logger)
    {
        _payerClient = payerClient;
        _repository = repository;
        _workflow = workflow.Value;
        _logger = logger;
    }

    /// <summary>Creates the Subscription at the payer unless one is already active for this organization. Never throws.</summary>
    public async Task EnsureSubscribedAsync(PayerSettings payer, string organizationNpi, CancellationToken ct)
    {
        if (!payer.UseSubscriptions)
        {
            return;
        }

        PayerSubscriptionRecord? existing = await _repository.GetAsync(payer.PayerName, organizationNpi, ct);
        if (existing != null && existing.Status == "active")
        {
            return;
        }

        Subscription subscription = new Subscription();
        subscription.Meta = new Meta();
        subscription.Meta.Profile.Add(PasProfiles.Subscription);
        subscription.Reason = "Prior authorization decision updates";
        subscription.Criteria = PasCodes.SubscriptionTopic;
        subscription.End = FhirDate.Instant(DateTime.UtcNow.AddYears(1));

        // only ClaimResponses for our organization (by NPI)
        Extension filter = new Extension(PasCodes.BackportFilterCriteria);
        filter.ValueString = "ClaimResponse?orgIdentifier=" + CodeSystems.Npi + "|" + organizationNpi;
        subscription.CriteriaExtensions = new PrimitiveExtensions();
        subscription.CriteriaExtensions.Extension.Add(filter);

        subscription.Channel = new SubscriptionChannel();
        subscription.Channel.Endpoint = _workflow.GetGatewayBaseUrl() + "/api/v1/pas/notifications/" + Uri.EscapeDataString(payer.PayerName);
        subscription.Channel.Payload = ApiHeaders.FhirJson;
        Extension payload = new Extension(PasCodes.BackportPayloadContent);
        payload.ValueCode = "id-only";
        subscription.Channel.PayloadExtensions = new PrimitiveExtensions();
        subscription.Channel.PayloadExtensions.Extension.Add(payload);
        if (!string.IsNullOrEmpty(payer.NotificationSecret))
        {
            subscription.Channel.Header.Add("Authorization: Bearer " + payer.NotificationSecret);
        }

        PayerSubscriptionRecord record = new PayerSubscriptionRecord();
        record.PayerName = payer.PayerName;
        record.OrganizationNpi = organizationNpi;
        try
        {
            PayerCallResult result = await _payerClient.PostFhirAsync(payer, "Subscription", subscription, ct);
            record.SubscriptionId = FhirJson.Id(result.Body);
            record.Status = result.IsSuccess() ? "active" : "error";
            if (!result.IsSuccess())
            {
                _logger.LogWarning("{Payer} refused the PAS Subscription (HTTP {Status}); the background poller will use $inquire", payer.PayerName, result.StatusCode);
            }
        }
        catch (PayerException ex)
        {
            record.Status = "error";
            _logger.LogWarning(ex, "PAS Subscription at {Payer} failed; the background poller will use $inquire", payer.PayerName);
        }
        await _repository.SaveAsync(record, ct);
    }
}
