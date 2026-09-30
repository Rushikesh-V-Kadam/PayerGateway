using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>CRD storage in Oracle: tables PA_CRD_EVENT and PA_COVERAGE_DECISION.</summary>
public class OracleCrdRepository : ICrdRepository
{
    private readonly OracleDb _db;

    /// <summary>Created by dependency injection.</summary>
    public OracleCrdRepository(OracleDb db)
    {
        _db = db;
    }

    public async Task<CrdEventRecord?> GetEventAsync(string eventId, CancellationToken ct)
    {
        const string sql = "SELECT EVENT_ID, PATIENT_ID, PAYER_NAME, RESPONSE_JSON, CREATED_AT FROM PA_CRD_EVENT WHERE EVENT_ID = :eventId";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("eventId", eventId) };
        List<CrdEventRecord> rows = await _db.QueryAsync(sql, parameters, ReadEvent, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task SaveEventAsync(CrdEventRecord record, CancellationToken ct)
    {
        // An event is written once; a repeated eventId is answered from the stored row before we get here
        const string sql = @"INSERT INTO PA_CRD_EVENT (EVENT_ID, PATIENT_ID, PAYER_NAME, RESPONSE_JSON, CREATED_AT)
                             VALUES (:eventId, :patientId, :payerName, :responseJson, :createdAt)";
        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("eventId", record.EventId),
            OracleDb.Text("patientId", record.PatientId),
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Clob("responseJson", record.ResponseJson),
            OracleDb.Time("createdAt", record.CreatedAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    public async Task<CoverageDecisionRecord?> GetDecisionAsync(string orderId, CancellationToken ct)
    {
        const string sql = @"SELECT ORDER_ID, EVENT_ID, PAYER_NAME, PATIENT_ID, COVERAGE_ASSERTION_ID, COVERED, PA_NEEDED, DOC_NEEDED,
                                    DECISION_JSON, UPDATED_AT
                               FROM PA_COVERAGE_DECISION WHERE ORDER_ID = :orderId";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("orderId", orderId) };
        List<CoverageDecisionRecord> rows = await _db.QueryAsync(sql, parameters, ReadDecision, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task SaveDecisionAsync(CoverageDecisionRecord record, CancellationToken ct)
    {
        // MERGE = insert a new order, or replace the decision of an order checked before
        const string sql = @"
MERGE INTO PA_COVERAGE_DECISION t
USING (SELECT :orderId AS ORDER_ID, :eventId AS EVENT_ID, :payerName AS PAYER_NAME, :patientId AS PATIENT_ID,
              :assertionId AS COVERAGE_ASSERTION_ID, :covered AS COVERED, :paNeeded AS PA_NEEDED, :docNeeded AS DOC_NEEDED,
              :decisionJson AS DECISION_JSON, :updatedAt AS UPDATED_AT FROM DUAL) s
   ON (t.ORDER_ID = s.ORDER_ID)
 WHEN MATCHED THEN UPDATE SET
      t.EVENT_ID = s.EVENT_ID, t.PAYER_NAME = s.PAYER_NAME, t.PATIENT_ID = s.PATIENT_ID,
      t.COVERAGE_ASSERTION_ID = s.COVERAGE_ASSERTION_ID, t.COVERED = s.COVERED, t.PA_NEEDED = s.PA_NEEDED,
      t.DOC_NEEDED = s.DOC_NEEDED, t.DECISION_JSON = s.DECISION_JSON, t.UPDATED_AT = s.UPDATED_AT
 WHEN NOT MATCHED THEN INSERT
      (ORDER_ID, EVENT_ID, PAYER_NAME, PATIENT_ID, COVERAGE_ASSERTION_ID, COVERED, PA_NEEDED, DOC_NEEDED, DECISION_JSON, UPDATED_AT)
      VALUES (s.ORDER_ID, s.EVENT_ID, s.PAYER_NAME, s.PATIENT_ID, s.COVERAGE_ASSERTION_ID, s.COVERED, s.PA_NEEDED,
              s.DOC_NEEDED, s.DECISION_JSON, s.UPDATED_AT)";
        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("orderId", record.OrderId),
            OracleDb.Text("eventId", record.EventId),
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Text("patientId", record.PatientId),
            OracleDb.Text("assertionId", record.CoverageAssertionId),
            OracleDb.Text("covered", record.Covered),
            OracleDb.Text("paNeeded", record.PriorAuthNeeded),
            OracleDb.Text("docNeeded", record.DocumentationNeeded),
            OracleDb.Clob("decisionJson", record.DecisionJson),
            OracleDb.Time("updatedAt", record.UpdatedAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    /// <summary>One PA_CRD_EVENT row -> record.</summary>
    private static CrdEventRecord ReadEvent(OracleDataReader row)
    {
        CrdEventRecord record = new CrdEventRecord();
        record.EventId = OracleDb.StrRequired(row, "EVENT_ID");
        record.PatientId = OracleDb.StrRequired(row, "PATIENT_ID");
        record.PayerName = OracleDb.StrRequired(row, "PAYER_NAME");
        record.ResponseJson = OracleDb.StrRequired(row, "RESPONSE_JSON");
        record.CreatedAt = OracleDb.Time(row, "CREATED_AT");
        return record;
    }

    /// <summary>One PA_COVERAGE_DECISION row -> record.</summary>
    private static CoverageDecisionRecord ReadDecision(OracleDataReader row)
    {
        CoverageDecisionRecord record = new CoverageDecisionRecord();
        record.OrderId = OracleDb.StrRequired(row, "ORDER_ID");
        record.EventId = OracleDb.StrRequired(row, "EVENT_ID");
        record.PayerName = OracleDb.Str(row, "PAYER_NAME");
        record.PatientId = OracleDb.StrRequired(row, "PATIENT_ID");
        record.CoverageAssertionId = OracleDb.Str(row, "COVERAGE_ASSERTION_ID");
        record.Covered = OracleDb.StrRequired(row, "COVERED");
        record.PriorAuthNeeded = OracleDb.Str(row, "PA_NEEDED");
        record.DocumentationNeeded = OracleDb.Str(row, "DOC_NEEDED");
        record.DecisionJson = OracleDb.StrRequired(row, "DECISION_JSON");
        record.UpdatedAt = OracleDb.Time(row, "UPDATED_AT");
        return record;
    }
}
