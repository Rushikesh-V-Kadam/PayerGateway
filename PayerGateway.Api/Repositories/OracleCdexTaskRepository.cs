using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Payer Tasks in Oracle: table PA_CDEX_TASK.</summary>
public class OracleCdexTaskRepository : ICdexTaskRepository
{
    private readonly OracleDb _db;

    /// <summary>Created by dependency injection.</summary>
    public OracleCdexTaskRepository(OracleDb db)
    {
        _db = db;
    }

    public async Task SaveAsync(CdexTaskRecord record, CancellationToken ct)
    {
        // MERGE = insert a new Task or update one seen before (the payer changes its status)
        const string sql = @"
MERGE INTO PA_CDEX_TASK t
USING (SELECT :payerName AS PAYER_NAME, :taskId AS TASK_ID, :requestId AS REQUEST_ID, :trackingId AS TRACKING_ID,
              :trackingSystem AS TRACKING_SYSTEM, :status AS STATUS, :dueDate AS DUE_DATE, :taskJson AS TASK_JSON,
              :receivedAt AS RECEIVED_AT FROM DUAL) s
   ON (t.PAYER_NAME = s.PAYER_NAME AND t.TASK_ID = s.TASK_ID)
 WHEN MATCHED THEN UPDATE SET
      t.REQUEST_ID = s.REQUEST_ID, t.TRACKING_ID = s.TRACKING_ID, t.TRACKING_SYSTEM = s.TRACKING_SYSTEM,
      t.STATUS = s.STATUS, t.DUE_DATE = s.DUE_DATE, t.TASK_JSON = s.TASK_JSON
 WHEN NOT MATCHED THEN INSERT (PAYER_NAME, TASK_ID, REQUEST_ID, TRACKING_ID, TRACKING_SYSTEM, STATUS, DUE_DATE, TASK_JSON, RECEIVED_AT)
      VALUES (s.PAYER_NAME, s.TASK_ID, s.REQUEST_ID, s.TRACKING_ID, s.TRACKING_SYSTEM, s.STATUS, s.DUE_DATE, s.TASK_JSON, s.RECEIVED_AT)";
        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Text("taskId", record.TaskId),
            OracleDb.Text("requestId", record.RequestId),
            OracleDb.Text("trackingId", record.TrackingId),
            OracleDb.Text("trackingSystem", record.TrackingSystem),
            OracleDb.Text("status", record.Status),
            OracleDb.Text("dueDate", record.DueDate),
            OracleDb.Clob("taskJson", record.TaskJson),
            OracleDb.Time("receivedAt", record.ReceivedAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    public async Task<List<CdexTaskRecord>> GetForRequestAsync(string requestId, CancellationToken ct)
    {
        const string sql = @"SELECT PAYER_NAME, TASK_ID, REQUEST_ID, TRACKING_ID, TRACKING_SYSTEM, STATUS, DUE_DATE, TASK_JSON, RECEIVED_AT
                               FROM PA_CDEX_TASK WHERE REQUEST_ID = :requestId ORDER BY RECEIVED_AT";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("requestId", requestId) };
        return await _db.QueryAsync(sql, parameters, ReadRow, ct);
    }

    /// <summary>One row -> record.</summary>
    private static CdexTaskRecord ReadRow(OracleDataReader row)
    {
        CdexTaskRecord record = new CdexTaskRecord();
        record.PayerName = OracleDb.StrRequired(row, "PAYER_NAME");
        record.TaskId = OracleDb.StrRequired(row, "TASK_ID");
        record.RequestId = OracleDb.StrRequired(row, "REQUEST_ID");
        record.TrackingId = OracleDb.StrRequired(row, "TRACKING_ID");
        record.TrackingSystem = OracleDb.Str(row, "TRACKING_SYSTEM");
        record.Status = OracleDb.Str(row, "STATUS");
        record.DueDate = OracleDb.Str(row, "DUE_DATE");
        record.TaskJson = OracleDb.StrRequired(row, "TASK_JSON");
        record.ReceivedAt = OracleDb.Time(row, "RECEIVED_AT");
        return record;
    }
}
