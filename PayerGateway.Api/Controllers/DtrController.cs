using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Exceptions;
using PayerGateway.Api.Filters;
using PayerGateway.Api.Models.Requests;
using PayerGateway.Api.Models.Responses;
using PayerGateway.Api.Services.Dtr;
using PayerGateway.Api.Services.Payers;

namespace PayerGateway.Api.Controllers;

/// <summary>
/// STEP 2 - DTR (documentation). Forms as plain JSON (no UI or payer app needed), plus the endpoints for payer
/// SMART DTR apps when payers are onboarded with one.
/// </summary>
[ApiController]
[Route("api/v1/dtr")]
public class DtrController : ControllerBase
{
    private readonly DtrService _dtr;

    /// <summary>Created by dependency injection.</summary>
    public DtrController(DtrService dtr)
    {
        _dtr = dtr;
    }

    // ================================================================== forms (use now)

    /// <summary>Gets the payer's questionnaire(s) for the orders, as simple questions.</summary>
    /// <remarks>
    /// Call when order-sign returned callDtr = true. The gateway calls the payer's Questionnaire/$questionnaire-package
    /// with the orders and coverage, and returns one form per questionnaire.
    ///
    /// Next: show each question (text, type, options, required, showWhen) on the EHR screen and send the answers to
    /// POST /api/v1/dtr/forms/{formId}/answers. During testing the answers can be sent from Swagger or Postman.
    /// </remarks>
    /// <param name="request">Payer, patient, orders and the questionnaire URLs from the order-sign decision.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The forms with their questions.</response>
    /// <response code="400">Invalid body, missing field, unknown payerName, or an order of another patient.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">An order was not found at the Provider FHIR API.</response>
    /// <response code="422">No active insurance, or the payer refused / returned no questionnaire.</response>
    /// <response code="502">The payer or the Provider FHIR API failed.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("forms")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(List<DtrForm>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<List<DtrForm>>> CreateForms([FromBody] DtrFormRequest request, CancellationToken ct)
    {
        List<DtrForm> forms = await _dtr.CreateFormsAsync(request, ct);
        return Ok(forms);
    }

    /// <summary>Reads a form: its questions and the answers saved so far.</summary>
    /// <param name="formId">Form id from POST /api/v1/dtr/forms, e.g. frm-6f1c2a9e.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The form.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown formId.</response>
    [HttpGet("forms/{formId}")]
    [Produces("application/json")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(DtrForm), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DtrForm>> GetForm(string formId, CancellationToken ct)
    {
        DtrForm form = await _dtr.GetFormAsync(formId, ct);
        return Ok(form);
    }

    /// <summary>Saves the answers of a form (builds and stores the FHIR QuestionnaireResponse).</summary>
    /// <remarks>
    /// Send one entry per question: { "linkId": "...", "values": ["..."] }. Values are text in the format of the question type
    /// (boolean "true", integer "12", date "2026-08-10", choice = the option code, quantity "12 mL/min").
    ///
    /// - Answers are merged with earlier ones; an entry with an empty values list removes that answer.
    /// - complete = true: all required (shown) questions must be answered, otherwise 422 lists them.
    /// - complete = false: saved as in-progress; missingRequired lists what is still open.
    ///
    /// The QuestionnaireResponse is saved at the Provider FHIR API. POST /api/v1/pas/requests attaches completed forms
    /// of the same orders automatically; for CDex send questionnaireResponseId in the attachments call.
    /// </remarks>
    /// <param name="formId">Form id from POST /api/v1/dtr/forms.</param>
    /// <param name="request">The answers.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">Saved; the QuestionnaireResponse id is returned.</response>
    /// <response code="400">An answer has the wrong format or is not one of the options (details list each one). Nothing saved.</response>
    /// <response code="401">Missing or wrong X-Api-Key.</response>
    /// <response code="404">Unknown formId.</response>
    /// <response code="422">complete = true but required questions are unanswered (details list them). Nothing saved.</response>
    /// <response code="502">The Provider FHIR API failed to save the form.</response>
    [HttpPost("forms/{formId}/answers")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(DtrAnswersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<DtrAnswersResult>> SaveAnswers(string formId, [FromBody] DtrAnswersRequest request, CancellationToken ct)
    {
        DtrAnswersResult result = await _dtr.SaveAnswersAsync(formId, request, ct);
        return Ok(result);
    }

    // ================================================================== payer SMART DTR apps (later)

    /// <summary>Opens a payer's SMART DTR app (link from an order-sign message, type "smart").</summary>
    /// <remarks>
    /// Opened in the clinician's browser (no API key). Creates the SMART launch at the Provider FHIR API and redirects
    /// (302) to the payer's DTR app. Shows a short HTML page when the link is invalid or expired.
    /// Only used once payers are onboarded with their own DTR app.
    /// </remarks>
    /// <param name="linkId">Id at the end of the link.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="302">Redirect to the payer DTR app.</response>
    /// <response code="404">Unknown or expired link (HTML page).</response>
    [HttpGet("launch/{linkId}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Launch(string linkId, CancellationToken ct)
    {
        try
        {
            string url = await _dtr.LaunchAsync(linkId, ct);
            return Redirect(url);
        }
        catch (ApiException ex)
        {
            string html = "<!doctype html><html><body style=\"font-family:sans-serif;margin:2rem\"><h2>Documentation link unavailable</h2>" +
                          "<p>" + WebUtility.HtmlEncode(ex.Message) + "</p></body></html>";
            ContentResult page = new ContentResult();
            page.Content = html;
            page.ContentType = "text/html";
            page.StatusCode = ex.StatusCode;
            return page;
        }
    }

    /// <summary>For payer SMART DTR apps: $questionnaire-package passed to the payer unchanged (body = FHIR Parameters).</summary>
    /// <param name="payerName">Payer name.</param>
    /// <param name="body">FHIR Parameters from the DTR app.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The payer's FHIR answer (the payer's own status code is passed through).</response>
    /// <response code="404">The payer is not configured.</response>
    /// <response code="502">The payer could not be reached.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("{payerName}/Questionnaire/$questionnaire-package")]
    [Consumes(ApiHeaders.FhirJson, ApiHeaders.Json)]
    [Produces(ApiHeaders.FhirJson)]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> RelayQuestionnairePackage(string payerName, [FromBody] JsonObject body, CancellationToken ct)
    {
        PayerCallResult result = await _dtr.RelayToPayerAsync(payerName, DtrCodes.QuestionnairePackage, body, ct);
        return FhirResult(result);
    }

    /// <summary>For payer SMART DTR apps: adaptive forms, $next-question passed to the payer unchanged.</summary>
    /// <param name="payerName">Payer name.</param>
    /// <param name="body">FHIR Parameters / QuestionnaireResponse from the DTR app.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">The payer's FHIR answer (the payer's own status code is passed through).</response>
    /// <response code="404">The payer is not configured.</response>
    /// <response code="502">The payer could not be reached.</response>
    /// <response code="504">The payer did not answer in time.</response>
    [HttpPost("{payerName}/Questionnaire/$next-question")]
    [Consumes(ApiHeaders.FhirJson, ApiHeaders.Json)]
    [Produces(ApiHeaders.FhirJson)]
    [ServiceFilter(typeof(EhrApiKeyFilter))]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> RelayNextQuestion(string payerName, [FromBody] JsonObject body, CancellationToken ct)
    {
        PayerCallResult result = await _dtr.RelayToPayerAsync(payerName, DtrCodes.NextQuestion, body, ct);
        return FhirResult(result);
    }

    /// <summary>Returns the payer's FHIR answer with the payer's status code.</summary>
    private static ContentResult FhirResult(PayerCallResult result)
    {
        ContentResult content = new ContentResult();
        content.Content = result.Body == null ? "{}" : result.Body.ToJsonString();
        content.ContentType = ApiHeaders.FhirJson;
        content.StatusCode = result.StatusCode;
        return content;
    }
}
