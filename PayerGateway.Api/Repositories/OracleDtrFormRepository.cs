using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Requests;

namespace PayerGateway.Api.Repositories;

/// <summary>DTR forms in Oracle: table PA_DTR_FORM.</summary>
public class OracleDtrFormRepository : IDtrFormRepository
{
    private readonly OracleDb _db;

    /// <summary>Created by dependency injection.</summary>
    public OracleDtrFormRepository(OracleDb db)
    {
        _db = db;
    }

    public async Task SaveAsync(DtrFormRecord record, CancellationToken ct)
    {
        record.UpdatedAt = DateTime.UtcNow;
        // MERGE = insert a new form, or update the status / answers of an existing one
        const string sql = @"
MERGE INTO PA_DTR_FORM t
USING (SELECT :formId AS FORM_ID, :payerName AS PAYER_NAME, :patientId AS PATIENT_ID, :coverageId AS COVERAGE_ID,
              :orderIds AS ORDER_IDS_JSON, :questionnaireUrl AS QUESTIONNAIRE_URL, :questionnaire AS QUESTIONNAIRE_JSON,
              :status AS STATUS, :qrId AS QUESTIONNAIRE_RESPONSE_ID, :answers AS ANSWERS_JSON,
              :adaptive AS ADAPTIVE, :payerFinished AS PAYER_FINISHED, :adaptiveResponse AS ADAPTIVE_RESPONSE_JSON,
              :createdAt AS CREATED_AT, :updatedAt AS UPDATED_AT FROM DUAL) s
   ON (t.FORM_ID = s.FORM_ID)
 WHEN MATCHED THEN UPDATE SET
      t.STATUS = s.STATUS, t.QUESTIONNAIRE_RESPONSE_ID = s.QUESTIONNAIRE_RESPONSE_ID, t.ANSWERS_JSON = s.ANSWERS_JSON,
      t.QUESTIONNAIRE_JSON = s.QUESTIONNAIRE_JSON, t.PAYER_FINISHED = s.PAYER_FINISHED, t.ADAPTIVE_RESPONSE_JSON = s.ADAPTIVE_RESPONSE_JSON,
      t.UPDATED_AT = s.UPDATED_AT
 WHEN NOT MATCHED THEN INSERT
      (FORM_ID, PAYER_NAME, PATIENT_ID, COVERAGE_ID, ORDER_IDS_JSON, QUESTIONNAIRE_URL, QUESTIONNAIRE_JSON, STATUS,
       QUESTIONNAIRE_RESPONSE_ID, ANSWERS_JSON, ADAPTIVE, PAYER_FINISHED, ADAPTIVE_RESPONSE_JSON, CREATED_AT, UPDATED_AT)
      VALUES (s.FORM_ID, s.PAYER_NAME, s.PATIENT_ID, s.COVERAGE_ID, s.ORDER_IDS_JSON, s.QUESTIONNAIRE_URL, s.QUESTIONNAIRE_JSON,
              s.STATUS, s.QUESTIONNAIRE_RESPONSE_ID, s.ANSWERS_JSON, s.ADAPTIVE, s.PAYER_FINISHED, s.ADAPTIVE_RESPONSE_JSON,
              s.CREATED_AT, s.UPDATED_AT)";
        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("formId", record.FormId),
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Text("patientId", record.PatientId),
            OracleDb.Text("coverageId", record.CoverageId),
            OracleDb.Clob("orderIds", ApiJson.Write(record.OrderIds)),
            OracleDb.Text("questionnaireUrl", record.QuestionnaireUrl),
            OracleDb.Clob("questionnaire", record.QuestionnaireJson),
            OracleDb.Text("status", record.Status),
            OracleDb.Text("qrId", record.QuestionnaireResponseId),
            OracleDb.Clob("answers", ApiJson.Write(record.Answers)),
            OracleDb.Number("adaptive", record.Adaptive ? 1 : 0),
            OracleDb.Number("payerFinished", record.PayerFinished ? 1 : 0),
            OracleDb.Clob("adaptiveResponse", record.AdaptiveResponseJson),
            OracleDb.Time("createdAt", record.CreatedAt),
            OracleDb.Time("updatedAt", record.UpdatedAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    public async Task<DtrFormRecord?> GetAsync(string formId, CancellationToken ct)
    {
        const string sql = @"SELECT FORM_ID, PAYER_NAME, PATIENT_ID, COVERAGE_ID, ORDER_IDS_JSON, QUESTIONNAIRE_URL, QUESTIONNAIRE_JSON,
                                    STATUS, QUESTIONNAIRE_RESPONSE_ID, ANSWERS_JSON, ADAPTIVE, PAYER_FINISHED,
                                    ADAPTIVE_RESPONSE_JSON, CREATED_AT, UPDATED_AT
                               FROM PA_DTR_FORM WHERE FORM_ID = :formId";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("formId", formId) };
        List<DtrFormRecord> rows = await _db.QueryAsync(sql, parameters, ReadRow, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>One row -> record.</summary>
    private static DtrFormRecord ReadRow(OracleDataReader row)
    {
        DtrFormRecord record = new DtrFormRecord();
        record.FormId = OracleDb.StrRequired(row, "FORM_ID");
        record.PayerName = OracleDb.StrRequired(row, "PAYER_NAME");
        record.PatientId = OracleDb.StrRequired(row, "PATIENT_ID");
        record.CoverageId = OracleDb.Str(row, "COVERAGE_ID");
        record.OrderIds = ApiJson.Read<List<string>>(OracleDb.Str(row, "ORDER_IDS_JSON"));
        record.QuestionnaireUrl = OracleDb.StrRequired(row, "QUESTIONNAIRE_URL");
        record.QuestionnaireJson = OracleDb.StrRequired(row, "QUESTIONNAIRE_JSON");
        record.Status = OracleDb.StrRequired(row, "STATUS");
        record.QuestionnaireResponseId = OracleDb.Str(row, "QUESTIONNAIRE_RESPONSE_ID");
        record.Answers = ApiJson.Read<List<DtrAnswerInput>>(OracleDb.Str(row, "ANSWERS_JSON"));
        record.Adaptive = OracleDb.Int(row, "ADAPTIVE") == 1;
        record.PayerFinished = OracleDb.Int(row, "PAYER_FINISHED") == 1;
        record.AdaptiveResponseJson = OracleDb.Str(row, "ADAPTIVE_RESPONSE_JSON");
        record.CreatedAt = OracleDb.Time(row, "CREATED_AT");
        record.UpdatedAt = OracleDb.Time(row, "UPDATED_AT");
        return record;
    }
}
