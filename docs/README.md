# Payer Gateway API (simple version)

Runs the Da Vinci prior authorization flow for the EHR against any connected payer:

| Step | EHR calls | Gateway calls the payer | When |
| --- | --- | --- | --- |
| 1 CRD | `POST /api/v1/crd/order-sign` | CDS Hooks `order-sign` | Every signed order |
| 2 DTR | `POST /api/v1/dtr/forms` → `POST /api/v1/dtr/forms/{formId}/answers` | `Questionnaire/$questionnaire-package` | Decision has `callDtr = true` |
| 3 PAS | `POST /api/v1/pas/requests` (+ PUT / cancel / GET / inquire) | `Claim/$submit`, `Claim/$inquire` | Decision has `callPas = true` |
| 4 CDex | `GET .../information-requests`, `POST .../attachments` | `Task` search, `$submit-attachment` | PAS is `pended` and the payer asks for more |

Full request / response specs with examples: run the API and open **http://localhost:5200/swagger**.
`PayerGateway.Api.http` has every call in order.

## How the payer is chosen

Every EHR request carries `payerName`. The gateway looks it up in `appsettings.json` → `Payers` (`PayerName`, not case-sensitive)
and uses that entry's `BaseUrl`, auth and timeouts. To onboard a payer, add an entry; nothing in the code changes.
An unknown name returns 400 with the list of configured payers.

## How the questionnaire is filled without a UI

Today there is no SMART DTR app, so the gateway does the DTR work itself and exposes it as plain JSON:

1. **Get the form.** `POST /api/v1/dtr/forms` with `payerName`, `patientId`, `orderIds` and the `questionnaireUrls` /
   `coverageAssertionId` from the order-sign decision. The gateway calls the payer's `$questionnaire-package` and returns each
   Questionnaire as a list of questions: `linkId`, `text`, `type`, `required`, `options`, `defaultValues`, `showWhen`.
2. **Answer it.** The EHR team shows those questions on an existing EHR screen (a simple form: one field per question).
   During testing the answers can be typed into Swagger or Postman.
3. **Save the answers.** `POST /api/v1/dtr/forms/{formId}/answers` with `{ "linkId": "...", "values": ["..."] }` per question.
   The gateway checks each value against the question type, skips questions hidden by `showWhen`, uses payer defaults
   for questions left empty, and (with `complete = true`) rejects the save with 422 when a required question is missing.
   It then builds the FHIR QuestionnaireResponse (same tree as the payer's Questionnaire, with `qr-context` for the orders
   and coverage) and saves it at the Provider FHIR API.
4. **Submit.** `POST /api/v1/pas/requests` attaches every completed form of the same orders automatically.

**Later, when payers bring their own SMART DTR app:** order-sign returns a message link of type `smart`
(`/api/v1/dtr/launch/{linkId}`). The EHR opens it in a browser; the gateway creates the SMART launch at the Provider FHIR API
and redirects to the payer's app. The app reads the patient data from the Provider FHIR API, gets the questionnaire through
`/api/v1/dtr/{payerName}/Questionnaire/$questionnaire-package`, and saves the QuestionnaireResponse at the Provider FHIR API.
PAS picks it up the same way, so steps 3 and 4 do not change.

## Errors

Every error has the same body: `{ "error": "...", "details": ["..."], "traceId": "..." }`.

| Status | Meaning |
| --- | --- |
| 400 | Invalid body / missing field / unknown payerName / wrong answer format |
| 401 | Missing or wrong `X-Api-Key` (only when `Security:EhrApiKey` is set) |
| 404 | Patient, order, form, document or request not found |
| 409 | Request already submitted (use PUT), or it is cancelled |
| 422 | Cannot be processed: no active insurance, no NPI, required questions missing, payer rejected (PAS returns the decision with errors) |
| 502 | Payer, Provider FHIR API or EHR failed |
| 503 | Oracle not available |
| 504 | Payer did not answer in time |
| 500 | Unexpected error (look up the traceId in the log) |

## Run locally

1. Start the Provider FHIR API (5100), the mock EHR (5090) and the mock payer (5080).
2. `dotnet run --project PayerGateway.Api` → http://localhost:5200/swagger
3. Storage is in memory by default. For Oracle: run `Database/01_create_tables.sql`, set `Storage:Provider` to `Oracle`
   and `ConnectionStrings:Oracle`.

## Code rules used

- Controllers only (no minimal API endpoints); `Program.cs` uses top-level statements and calls `Startup`.
- No lambdas: plain loops and named methods (option setup uses named methods such as `ConfigureSwagger`).
- One class per file, a comment on every class and method, all FHIR / CDS Hooks models written in the project.
