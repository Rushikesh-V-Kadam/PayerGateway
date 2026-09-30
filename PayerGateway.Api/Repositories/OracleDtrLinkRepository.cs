using Oracle.ManagedDataAccess.Client;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Models.Entities;

namespace PayerGateway.Api.Repositories;

/// <summary>Payer SMART DTR app links in Oracle: table PA_DTR_LINK.</summary>
public class OracleDtrLinkRepository : IDtrLinkRepository
{
    private readonly OracleDb _db;

    /// <summary>Created by dependency injection.</summary>
    public OracleDtrLinkRepository(OracleDb db)
    {
        _db = db;
    }

    public async Task SaveAsync(DtrLinkRecord record, CancellationToken ct)
    {
        const string sql = @"INSERT INTO PA_DTR_LINK
                                (LINK_ID, PAYER_NAME, PATIENT_ID, ENCOUNTER_ID, USER_PRACTITIONER_ID, FHIR_CONTEXT_JSON, APP_CONTEXT, APP_URL, EXPIRES_AT)
                             VALUES (:linkId, :payerName, :patientId, :encounterId, :userId, :fhirContext, :appContext, :appUrl, :expiresAt)";
        OracleParameter[] parameters = new OracleParameter[]
        {
            OracleDb.Text("linkId", record.LinkId),
            OracleDb.Text("payerName", record.PayerName),
            OracleDb.Text("patientId", record.PatientId),
            OracleDb.Text("encounterId", record.EncounterId),
            OracleDb.Text("userId", record.UserPractitionerId),
            OracleDb.Clob("fhirContext", ApiJson.Write(record.FhirContext)),
            OracleDb.Clob("appContext", record.AppContext),
            OracleDb.Text("appUrl", record.AppUrl),
            OracleDb.Time("expiresAt", record.ExpiresAt)
        };
        await _db.ExecuteAsync(sql, parameters, ct);
    }

    public async Task<DtrLinkRecord?> GetAsync(string linkId, CancellationToken ct)
    {
        const string sql = @"SELECT LINK_ID, PAYER_NAME, PATIENT_ID, ENCOUNTER_ID, USER_PRACTITIONER_ID, FHIR_CONTEXT_JSON, APP_CONTEXT, APP_URL, EXPIRES_AT
                               FROM PA_DTR_LINK WHERE LINK_ID = :linkId";
        OracleParameter[] parameters = new OracleParameter[] { OracleDb.Text("linkId", linkId) };
        List<DtrLinkRecord> rows = await _db.QueryAsync(sql, parameters, ReadRow, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>One row -> record.</summary>
    private static DtrLinkRecord ReadRow(OracleDataReader row)
    {
        DtrLinkRecord record = new DtrLinkRecord();
        record.LinkId = OracleDb.StrRequired(row, "LINK_ID");
        record.PayerName = OracleDb.StrRequired(row, "PAYER_NAME");
        record.PatientId = OracleDb.StrRequired(row, "PATIENT_ID");
        record.EncounterId = OracleDb.Str(row, "ENCOUNTER_ID");
        record.UserPractitionerId = OracleDb.Str(row, "USER_PRACTITIONER_ID");
        record.FhirContext = ApiJson.Read<List<string>>(OracleDb.Str(row, "FHIR_CONTEXT_JSON"));
        record.AppContext = OracleDb.Str(row, "APP_CONTEXT");
        record.AppUrl = OracleDb.StrRequired(row, "APP_URL");
        record.ExpiresAt = OracleDb.Time(row, "EXPIRES_AT");
        return record;
    }
}
