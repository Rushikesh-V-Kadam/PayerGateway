using Oracle.ManagedDataAccess.Client;

namespace PayerGateway.Api.Repositories;

/// <summary>
/// Small helper around ODP.NET so each repository only contains its SQL:
/// open a pooled connection, run a statement, read rows, build parameters.
/// Parameters are bound by name (:id, :status ...).
/// </summary>
public class OracleDb
{
    private readonly string _connectionString;

    /// <summary>Reads ConnectionStrings:Oracle.</summary>
    public OracleDb(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Oracle");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Oracle is missing in appsettings.json.");
        }
        _connectionString = connectionString;
    }

    /// <summary>Runs INSERT / UPDATE / MERGE / DELETE.</summary>
    public async Task ExecuteAsync(string sql, OracleParameter[] parameters, CancellationToken ct)
    {
        using (OracleConnection connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync(ct);
            using (OracleCommand command = NewCommand(connection, sql, parameters))
            {
                await command.ExecuteNonQueryAsync(ct);
            }
        }
    }

    /// <summary>Runs a SELECT and converts every row with <paramref name="readRow"/> (a repository method).</summary>
    public async Task<List<T>> QueryAsync<T>(string sql, OracleParameter[] parameters, Func<OracleDataReader, T> readRow, CancellationToken ct)
    {
        List<T> rows = new List<T>();
        using (OracleConnection connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync(ct);
            using (OracleCommand command = NewCommand(connection, sql, parameters))
            using (OracleDataReader reader = (OracleDataReader)await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    rows.Add(readRow(reader));
                }
            }
        }
        return rows;
    }

    // ------------------------------------------------------------------ parameter builders

    /// <summary>VARCHAR2 parameter (null -> DB NULL).</summary>
    public static OracleParameter Text(string name, string? value)
    {
        OracleParameter parameter = new OracleParameter(name, OracleDbType.Varchar2);
        parameter.Value = value == null ? DBNull.Value : value;
        return parameter;
    }

    /// <summary>CLOB parameter for JSON (no 4000-character limit).</summary>
    public static OracleParameter Clob(string name, string? value)
    {
        OracleParameter parameter = new OracleParameter(name, OracleDbType.Clob);
        parameter.Value = value == null ? DBNull.Value : value;
        return parameter;
    }

    /// <summary>NUMBER parameter.</summary>
    public static OracleParameter Number(string name, int value)
    {
        OracleParameter parameter = new OracleParameter(name, OracleDbType.Int32);
        parameter.Value = value;
        return parameter;
    }

    /// <summary>TIMESTAMP parameter (UTC).</summary>
    public static OracleParameter Time(string name, DateTime value)
    {
        OracleParameter parameter = new OracleParameter(name, OracleDbType.TimeStamp);
        parameter.Value = value;
        return parameter;
    }

    // ------------------------------------------------------------------ column readers

    /// <summary>String column, or null.</summary>
    public static string? Str(OracleDataReader row, string column)
    {
        int index = row.GetOrdinal(column);
        if (row.IsDBNull(index))
        {
            return null;
        }
        return row.GetString(index);
    }

    /// <summary>String column that must have a value (empty string when NULL).</summary>
    public static string StrRequired(OracleDataReader row, string column)
    {
        string? value = Str(row, column);
        return value ?? string.Empty;
    }

    /// <summary>Number column (0 when NULL).</summary>
    public static int Int(OracleDataReader row, string column)
    {
        int index = row.GetOrdinal(column);
        if (row.IsDBNull(index))
        {
            return 0;
        }
        return Convert.ToInt32(row.GetDecimal(index));
    }

    /// <summary>Timestamp column.</summary>
    public static DateTime Time(OracleDataReader row, string column)
    {
        return row.GetDateTime(row.GetOrdinal(column));
    }

    /// <summary>Creates a command that binds parameters by name.</summary>
    private static OracleCommand NewCommand(OracleConnection connection, string sql, OracleParameter[] parameters)
    {
        OracleCommand command = new OracleCommand(sql, connection);
        command.BindByName = true;
        command.Parameters.AddRange(parameters);
        return command;
    }
}
