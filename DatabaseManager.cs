using System.Data;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace DllInspectorMcp;

public class DatabaseManager : IDisposable
{
    private readonly SqliteConnection _connection;
    private const string DefaultDatabaseName = "civil3d_api.db";

    public DatabaseManager(string databasePath = null)
    {
        databasePath ??= Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            DefaultDatabaseName
        );

        var connectionString = $"Data Source={databasePath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public void InitializeDatabase()
    {
        var schemaPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "DatabaseSchema.sql"
        );

        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"Schema file not found at: {schemaPath}");
        }

        var schema = File.ReadAllText(schemaPath);
        ExecuteNonQuery(schema);
    }

    public void ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        command.ExecuteNonQuery();
    }

    public long ExecuteInsert(string sql, params SqliteParameter[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        command.ExecuteNonQuery();

        command.CommandText = "SELECT last_insert_rowid()";
        command.Parameters.Clear();
        return (long)command.ExecuteScalar()!;
    }

    public object? ExecuteScalar(string sql, params SqliteParameter[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }
        return command.ExecuteScalar();
    }

    public List<Dictionary<string, object?>> ExecuteQuery(string sql, params SqliteParameter[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }

        var results = new List<Dictionary<string, object?>>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    public SqliteTransaction BeginTransaction()
    {
        return _connection.BeginTransaction();
    }

    public bool IsAssemblyAnalyzed(string assemblyFullName)
    {
        var result = ExecuteScalar(
            "SELECT COUNT(*) FROM Assemblies WHERE FullName = @fullName",
            new SqliteParameter("@fullName", assemblyFullName)
        );
        return Convert.ToInt64(result) > 0;
    }

    public string? GetAssemblyHash(string assemblyFullName)
    {
        var result = ExecuteScalar(
            "SELECT FileHash FROM Assemblies WHERE FullName = @fullName",
            new SqliteParameter("@fullName", assemblyFullName)
        );
        return result?.ToString();
    }

    public void DeleteAssemblyData(string assemblyFullName)
    {
        ExecuteNonQuery(
            "DELETE FROM Assemblies WHERE FullName = @fullName",
            new SqliteParameter("@fullName", assemblyFullName)
        );
    }

    public static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
