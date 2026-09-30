using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Entities;
using PayerGateway.Api.Models.Responses;

namespace PayerGateway.Api.Repositories;

/// <summary>Prior auth requests in Oracle: table PA_PRIOR_AUTH. Lists and the decision are stored as JSON (CLOB).</summary>
public class OraclePriorAuthRepository : IPriorAuthRepository
{
    private const string Columns = @"REQUEST_ID, PAYER_NAME, PATIENT_ID, COVERAGE_ID, MEMBER_ID, ORGANIZATION_ID, ORGANIZATION_NPI,
        PRACTITIONER_ID, PRACTITIONER_NPI, PRIORITY, STATUS, AUTHORIZATION_NUMBER, VERSION_NO, ORDER_IDS_JSON, LINE_NUMBERS_JSON,
        DECISION_JSON, LAST_REQUEST_BUNDLE, LAST_RESPONSE_BUNDLE, EHR_SYNC_STATUS, SUBMITTED_AT, UPDATED_AT";

    private readonly OracleDb _db;

    /// <summary>Created by dependency injection.</summary>
    public OraclePriorAuthRepository(OracleDb db)
    {
        _db = db;
    }

    public async Task<PriorAuthRecord?> GetAsync(string requestId, CancellationToken ct)
    {
        string sql = "SELECT " + Columns + " FROM PA_PRIOR_AUTH WHERE REQUEST_ID = :requestId";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("requestId", requestId) };
        List<PriorAuthRecord> rows = await _db.QueryAsync(sql, parameters, ReadRow, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<List<PriorAuthRecord>> GetOpenAsync(string? payerName, CancellationToken ct)
    {
        if (payerName == null)
        {
            string allSql = "SELECT " + Columns + " FROM PA_PRIOR_AUTH WHERE STATUS = 'pended'";
            return await _db.QueryAsync(allSql, new OracleParameter[0], ReadRow, ct);
        }

        string sql = "SELECT " + Columns + " FROM PA_PRIOR_AUTH WHERE STATUS = 'pended' AND UPPER(PAYER_NAME) = UPPER(:payerName)";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("payerName", payerName) };
        return await _db.QueryAsync(sql, parameters, ReadRow, ct);
    }

    public async Task<List<PriorAuthRecord>> GetEhrSyncFailedAsync(CancellationToken ct)
    {
        string sql = "SELECT " + Columns + " FROM PA_PRIOR_AUTH WHERE EHR_SYNC_STATUS = 'FAILED'";
        return await _db.QueryAsync(sql, new OracleParameter[0], ReadRow, ct);
    }

    public async Task SaveAsync(PriorAuthRecord record, CancellationToken ct)
    {
        record.UpdatedAt = DateTime.UtcNow;
        // MERGE = insert a new request or update an existing one. Every value comes in once through the USING row "s".
        const string sql = @"
MERGE INTO PA_PRIOR_AUTH t
USING (SELECT :requestId AS REQUEST_ID, :payerName AS PAYER_NAME, :patientId AS PATIENT_ID, :coverageId AS COVERAGE_ID,
              :memberId AS MEMBER_ID, :organizationId AS ORGANIZATION_ID, :organizationNpi AS ORGANIZATION_NPI,
              :practitionerId AS PRACTITIONER_ID, :practitionerNpi AS PRACTITIONER_NPI, :priority AS PRIORITY,
              :status AS STATUS, :authNumber AS AUTHORIZATION_NUMBER, :versionNo AS VERSION_NO,
              :orderIds AS ORDER_IDS_JSON, :lineNumbers AS LINE_NUMBERS_JSON, :decision AS DECISION_JSON,
              :lastRequest AS LAST_REQUEST_BUNDLE, :lastResponse AS LAST_RESPONSE_BUNDLE, :ehrSync AS EHR_SYNC_STATUS,
              :submittedAt AS SUBMITTED_AT, :updatedAt AS UPDATED_AT
         FROM DUAL) s
   ON (t.REQUEST_ID = s.REQUEST_ID)
 WHEN MATCHED THEN UPDATE SET
      t.PAYER_NAME = s.PAYER_NAME, t.PATIENT_ID = s.PATIENT_ID, t.COVERAGE_ID = s.COVERAGE_ID, t.MEMBER_ID = s.MEMBER_ID,
      t.ORGANIZATION_ID = s.ORGANIZATION_ID, t.ORGANIZATION_NPI = s.ORGANIZATION_NPI, t.PRACTITIONER_ID = s.PRACTITIONER_ID,
      t.PRACTITIONER_NPI = s.PRACTITIONER_NPI, t.PRIORITY = s.PRIORITY, t.STATUS = s.STATUS,
      t.AUTHORIZATION_NUMBER = s.AUTHORIZATION_NUMBER, t.VERSION_NO = s.VERSION_NO, t.ORDER_IDS_JSON = s.ORDER_IDS_JSON,
      t.LINE_NUMBERS_JSON = s.LINE_NUMBERS_JSON, t.DECISION_JSON = s.DECISION_JSON, t.LAST_REQUEST_BUNDLE = s.LAST_REQUEST_BUNDLE,
      t.LAST_RESPONSE_BUNDLE = s.LAST_RESPONSE_BUNDLE, t.EHR_SYNC_STATUS = s.EHR_SYNC_STATUS, t.UPDATED_AT = s.UPDATED_AT
 WHEN NOT MATCHED THEN INSERT (REQUEST_ID, PAYER_NAME, PATIENT_ID, COVERAGE_ID, MEMBER_ID, ORGANIZATION_ID, ORGANIZATION_NPI,
      PRACTITIONER_ID, PRACTITIONER_NPI, PRIORITY, STATUS, AUTHORIZATION_NUMBER, VERSION_NO, ORDER_IDS_JSON, LINE_NUMBERS_JSON,
      DECISION_JSON, LAST_REQUEST_BUNDLE, LAST_RESPONSE_BUNDLE, EHR_SYNC_STATUS, SUBMITTED_AT, UPDATED_AT)
      VALUES (s.REQUEST_ID, s.PAYER_NAME, s.PATIENT_ID, s.COVERAGE_ID, s.MEMBER_ID, s.ORGANIZATION_ID, s.ORGANIZATION_NPI,
      s.PRACTITIONER_ID, s.PRACTITIONER_NPI, s.PRIORITY, s.STATUS, s.AUTHORIZATION_NUMBER, s.VERSION_NO, s.ORDER_IDS_JSON,
      s.LINE_NUMBERS_JSON, s.DECISION_JSON, s.LAST_REQUEST_BUNDLE, s.LAST_RESPONSE_BUNDLE, s.EHR_SYNC_STATUS, s.SUBMITTED_AT, s.UPDATED_AT)";

        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("requestId", record.RequestId),
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Text("patientId", record.PatientId),
            OracleDb.Text("coverageId", record.CoverageId),
            OracleDb.Text("memberId", record.MemberId),
            OracleDb.Text("organizationId", record.OrganizationId),
            OracleDb.Text("organizationNpi", record.OrganizationNpi),
            OracleDb.Text("practitionerId", record.PractitionerId),
            OracleDb.Text("practitionerNpi", record.PractitionerNpi),
            OracleDb.Text("priority", record.Priority),
            OracleDb.Text("status", record.Status),
            OracleDb.Text("authNumber", record.AuthorizationNumber),
            OracleDb.Number("versionNo", record.Version),
            OracleDb.Clob("orderIds", ApiJson.Write(record.OrderIds)),
            OracleDb.Clob("lineNumbers", ApiJson.Write(record.LineNumbers)),
            OracleDb.Clob("decision", ApiJson.Write(record.Decision)),
            OracleDb.Clob("lastRequest", record.LastRequestBundleJson),
            OracleDb.Clob("lastResponse", record.LastResponseBundleJson),
            OracleDb.Text("ehrSync", record.EhrSyncStatus),
            OracleDb.Time("submittedAt", record.SubmittedAt),
            OracleDb.Time("updatedAt", record.UpdatedAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    /// <summary>One row -> record.</summary>
    private static PriorAuthRecord ReadRow(OracleDataReader row)
    {
        PriorAuthRecord record = new PriorAuthRecord();
        record.RequestId = OracleDb.StrRequired(row, "REQUEST_ID");
        record.PayerName = OracleDb.StrRequired(row, "PAYER_NAME");
        record.PatientId = OracleDb.StrRequired(row, "PATIENT_ID");
        record.CoverageId = OracleDb.StrRequired(row, "COVERAGE_ID");
        record.MemberId = OracleDb.Str(row, "MEMBER_ID");
        record.OrganizationId = OracleDb.StrRequired(row, "ORGANIZATION_ID");
        record.OrganizationNpi = OracleDb.Str(row, "ORGANIZATION_NPI");
        record.PractitionerId = OracleDb.Str(row, "PRACTITIONER_ID");
        record.PractitionerNpi = OracleDb.Str(row, "PRACTITIONER_NPI");
        record.Priority = OracleDb.StrRequired(row, "PRIORITY");
        record.Status = OracleDb.StrRequired(row, "STATUS");
        record.AuthorizationNumber = OracleDb.Str(row, "AUTHORIZATION_NUMBER");
        record.Version = OracleDb.Int(row, "VERSION_NO");
        record.OrderIds = ApiJson.Read<List<string>>(OracleDb.Str(row, "ORDER_IDS_JSON"));
        record.LineNumbers = ApiJson.Read<Dictionary<string, int>>(OracleDb.Str(row, "LINE_NUMBERS_JSON"));
        record.Decision = ApiJson.Read<PriorAuthDecision>(OracleDb.Str(row, "DECISION_JSON"));
        record.LastRequestBundleJson = OracleDb.Str(row, "LAST_REQUEST_BUNDLE");
        record.LastResponseBundleJson = OracleDb.Str(row, "LAST_RESPONSE_BUNDLE");
        record.EhrSyncStatus = OracleDb.StrRequired(row, "EHR_SYNC_STATUS");
        record.SubmittedAt = OracleDb.Time(row, "SUBMITTED_AT");
        record.UpdatedAt = OracleDb.Time(row, "UPDATED_AT");
        return record;
    }
}
