using System.Text;
using Microsoft.Extensions.Options;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Clients;

/// <summary>
/// Writes decisions back to the EHR. This is the only way the gateway talks to the EHR directly
/// (all reads go through the Provider FHIR API):
///   PUT api/orders/{orderId}/coverage-decision     CRD decision of an order
///   PUT api/prior-auth-requests/{requestId}        PAS decision (includes CDex information requests)
/// </summary>
public class EhrClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly EhrSettings _settings;

    /// <summary>Created by dependency injection.</summary>
    public EhrClient(IHttpClientFactory httpFactory, IOptions<EhrSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    /// <summary>Sends the CRD decision of one order.</summary>
    public async Task PutCoverageDecisionAsync(CoverageDecision decision, CancellationToken ct)
    {
        string path = "api/orders/" + Uri.EscapeDataString(decision.OrderId) + "/coverage-decision";
        await PutAsync(path, ApiJson.Write(decision), ct);
    }

    /// <summary>Sends the prior authorization decision.</summary>
    public async Task PutPriorAuthDecisionAsync(PriorAuthDecision decision, CancellationToken ct)
    {
        string path = "api/prior-auth-requests/" + Uri.EscapeDataString(decision.RequestId);
        await PutAsync(path, ApiJson.Write(decision), ct);
    }

    /// <summary>PUT with JSON and the configured timeout; any failure becomes EhrException.</summary>
    private async Task PutAsync(string path, string json, CancellationToken ct)
    {
        string url = _settings.BaseUrl.TrimEnd('/') + "/" + path;
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url))
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            request.Content = new StringContent(json, Encoding.UTF8, ApiHeaders.Json);
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                request.Headers.Add(ApiHeaders.EhrApiKey, _settings.ApiKey);
            }
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));

            try
            {
                HttpClient http = _httpFactory.CreateClient();
                using (HttpResponseMessage response = await http.SendAsync(request, timeout.Token))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new EhrException("EHR PUT " + path + " returned HTTP " + (int)response.StatusCode);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                throw new EhrException("EHR PUT " + path + " did not answer within " + _settings.TimeoutSeconds + " seconds.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new EhrException("EHR PUT " + path + " failed: " + ex.Message, ex);
            }
        }
    }
}
