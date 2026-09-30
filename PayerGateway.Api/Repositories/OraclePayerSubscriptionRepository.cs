using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>PAS Subscriptions in Oracle: table PA_PAYER_SUBSCRIPTION.</summary>
public class OraclePayerSubscriptionRepository : IPayerSubscriptionRepository
{
    private readonly OracleDb _db;

    /// <summary>Created by dependency injection.</summary>
    public OraclePayerSubscriptionRepository(OracleDb db)
    {
        _db = db;
    }

    public async Task<PayerSubscriptionRecord?> GetAsync(string payerName, string organizationNpi, CancellationToken ct)
    {
        const string sql = @"SELECT PAYER_NAME, ORGANIZATION_NPI, SUBSCRIPTION_ID, STATUS, CREATED_AT FROM PA_PAYER_SUBSCRIPTION
                              WHERE PAYER_NAME = :payerName AND ORGANIZATION_NPI = :npi";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("payerName", payerName), OracleDb.Text("npi", organizationNpi) };
        List<PayerSubscriptionRecord> rows = await _db.QueryAsync(sql, parameters, ReadRow, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task SaveAsync(PayerSubscriptionRecord record, CancellationToken ct)
    {
        const string sql = @"
MERGE INTO PA_PAYER_SUBSCRIPTION t
USING (SELECT :payerName AS PAYER_NAME, :npi AS ORGANIZATION_NPI, :subscriptionId AS SUBSCRIPTION_ID, :status AS STATUS,
              :createdAt AS CREATED_AT FROM DUAL) s
   ON (t.PAYER_NAME = s.PAYER_NAME AND t.ORGANIZATION_NPI = s.ORGANIZATION_NPI)
 WHEN MATCHED THEN UPDATE SET t.SUBSCRIPTION_ID = s.SUBSCRIPTION_ID, t.STATUS = s.STATUS
 WHEN NOT MATCHED THEN INSERT (PAYER_NAME, ORGANIZATION_NPI, SUBSCRIPTION_ID, STATUS, CREATED_AT)
      VALUES (s.PAYER_NAME, s.ORGANIZATION_NPI, s.SUBSCRIPTION_ID, s.STATUS, s.CREATED_AT)";
        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Text("npi", record.OrganizationNpi),
            OracleDb.Text("subscriptionId", record.SubscriptionId),
            OracleDb.Text("status", record.Status),
            OracleDb.Time("createdAt", record.CreatedAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    /// <summary>One row -> record.</summary>
    private static PayerSubscriptionRecord ReadRow(OracleDataReader row)
    {
        PayerSubscriptionRecord record = new PayerSubscriptionRecord();
        record.PayerName = OracleDb.StrRequired(row, "PAYER_NAME");
        record.OrganizationNpi = OracleDb.StrRequired(row, "ORGANIZATION_NPI");
        record.SubscriptionId = OracleDb.Str(row, "SUBSCRIPTION_ID");
        record.Status = OracleDb.StrRequired(row, "STATUS");
        record.CreatedAt = OracleDb.Time(row, "CREATED_AT");
        return record;
    }
}
