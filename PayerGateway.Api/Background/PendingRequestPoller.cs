using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Repositories;
using PayerGateway.Api.Services.Cdex;
using PayerGateway.Api.Services.Pas;

namespace PayerGateway.Api.Background;

/// <summary>
/// Safety net that runs every Workflow:PollMinutes (0 = off). Notifications from payers are the fast path; this job
/// makes sure nothing is missed:
///   1. retry EHR write-backs that failed
///   2. look for new payer information requests (CDex Tasks) for pended requests
///   3. ask the payer about every pended request ($inquire)
/// One failure never stops the job: it is logged and the next item / next run continues.
/// </summary>
public class PendingRequestPoller : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WorkflowSettings _workflow;
    private readonly ILogger<PendingRequestPoller> _logger;

    /// <summary>Created once at startup.</summary>
    public PendingRequestPoller(IServiceScopeFactory scopes, IOptions<WorkflowSettings> workflow, ILogger<PendingRequestPoller> logger)
    {
        _scopes = scopes;
        _workflow = workflow.Value;
        _logger = logger;
    }

    /// <summary>The loop: wait, run once, repeat until the app stops.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_workflow.PollMinutes <= 0)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_workflow.PollMinutes), stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;   // the app is stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending request poll failed; will try again next time");
            }
        }
    }

    /// <summary>One pass over failed write-backs and pended requests.</summary>
    private async Task RunOnceAsync(CancellationToken ct)
    {
        // Services are "scoped", so a new scope is created for each pass
        using (IServiceScope scope = _scopes.CreateScope())
        {
            IPriorAuthRepository repository = scope.ServiceProvider.GetRequiredService<IPriorAuthRepository>();
            PasService pas = scope.ServiceProvider.GetRequiredService<PasService>();
            CdexService cdex = scope.ServiceProvider.GetRequiredService<CdexService>();

            // 1. failed write-backs
            foreach (PriorAuthRecord record in await repository.GetEhrSyncFailedAsync(ct))
            {
                await pas.WriteBackAsync(record, ct);
            }

            // 2. Tasks: one search per payer + organization
            List<PriorAuthRecord> open = await repository.GetOpenAsync(null, ct);
            List<string> searched = new List<string>();
            foreach (PriorAuthRecord record in open)
            {
                if (record.OrganizationNpi == null)
                {
                    continue;
                }
                string key = record.PayerName.ToLowerInvariant() + "|" + record.OrganizationNpi;
                if (searched.Contains(key))
                {
                    continue;
                }
                searched.Add(key);
                await RunSafelyAsync("Task search at " + record.PayerName, cdex.PollTasksAsync(record.PayerName, record.OrganizationNpi, ct));
            }

            // 3. latest decisions
            foreach (PriorAuthRecord record in open)
            {
                await RunSafelyAsync("Inquiry of " + record.RequestId, pas.InquireAsync(record.RequestId, true, ct));
            }

            if (open.Count > 0)
            {
                _logger.LogInformation("Poll: {Count} pended request(s) checked", open.Count);
            }
        }
    }

    /// <summary>Waits for one piece of work; a failure is logged and does not stop the pass.</summary>
    private async Task RunSafelyAsync(string label, Task work)
    {
        try
        {
            await work;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Label} failed", label);
        }
    }
}
