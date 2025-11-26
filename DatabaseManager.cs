using System.Data;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace DllInspectorMcp;

public class DatabaseManager : IDisposable
{
    private readonly SqliteConnection _connection;
    private const string DefaultDatabaseName = "dll_inspector.db";

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

        // Run migration to v2 if needed
        MigrateToV2IfNeeded();
    }

    private void MigrateToV2IfNeeded()
    {
        // Check if Software table exists
        var tableExists = ExecuteScalar(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Software'"
        );

        if (Convert.ToInt64(tableExists) > 0)
        {
            return; // Already migrated
        }

        Console.WriteLine("Migrating database to v2 (adding Software table)...");

        // Step 1: Create Software table
        ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS Software (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Version TEXT,
                Description TEXT,
                CreatedDate TEXT NOT NULL
            )
        ");

        // Step 2: Add SoftwareId column to Assemblies
        try
        {
            ExecuteNonQuery(@"
                ALTER TABLE Assemblies ADD COLUMN SoftwareId INTEGER
                REFERENCES Software(Id) ON DELETE SET NULL
            ");
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
        {
            // Column already exists, skip
        }

        // Step 3: Create index
        ExecuteNonQuery(@"
            CREATE INDEX IF NOT EXISTS idx_assemblies_software ON Assemblies(SoftwareId)
        ");

        // Step 4: Recreate vw_TypesDetail view
        ExecuteNonQuery("DROP VIEW IF EXISTS vw_TypesDetail");
        ExecuteNonQuery(@"
            CREATE VIEW vw_TypesDetail AS
            SELECT
                t.Id,
                t.Name,
                t.FullName,
                t.TypeKind,
                t.IsAbstract,
                t.IsSealed,
                t.IsStatic,
                n.Name AS Namespace,
                a.Name AS Assembly,
                a.Version AS AssemblyVersion,
                s.Name AS Software,
                s.Version AS SoftwareVersion,
                bt.FullName AS BaseType,
                t.Summary
            FROM Types t
            JOIN Namespaces n ON t.NamespaceId = n.Id
            JOIN Assemblies a ON n.AssemblyId = a.Id
            LEFT JOIN Software s ON a.SoftwareId = s.Id
            LEFT JOIN Types bt ON t.BaseTypeId = bt.Id
        ");

        Console.WriteLine("Migration to v2 completed successfully.");
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
