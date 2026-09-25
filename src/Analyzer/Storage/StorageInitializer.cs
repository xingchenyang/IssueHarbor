using Microsoft.Data.Sqlite;

namespace IssueHarbor.Analyzer.Storage;

public sealed class StorageInitializer
{
    private readonly SqliteConnectionFactory _connections;
    private readonly string _migrationsDirectory;

    public StorageInitializer(SqliteConnectionFactory connections, string migrationsDirectory)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsDirectory);
        _migrationsDirectory = migrationsDirectory;
    }

    public void InitializeOrVerify()
    {
        var migrations = StorageMigrationLoader.Load(_migrationsDirectory);
        using var connection = _connections.OpenConnection();

        var tableCount = ScalarInt64(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';");
        var schemaVersion = ReadSchemaVersion(connection);

        if (tableCount == 0)
        {
            if (schemaVersion != 0)
            {
                throw new InvalidOperationException("Empty storage has a non-zero schema version.");
            }

            foreach (var migration in migrations)
            {
                ApplyMigration(connection, migration);
            }
        }

        Verify(connection, migrations);
    }

    private static void ApplyMigration(SqliteConnection connection, StorageMigration migration)
    {
        using var transaction = connection.BeginTransaction();

        using (var applyCommand = connection.CreateCommand())
        {
            applyCommand.Transaction = transaction;
            applyCommand.CommandText = migration.Sql;
            applyCommand.ExecuteNonQuery();
        }

        using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.Transaction = transaction;
            ledgerCommand.CommandText = """
                INSERT INTO schema_migrations (version, name, sha256, applied_at_utc)
                VALUES ($version, $name, $sha256, $applied_at_utc);
                """;
            ledgerCommand.Parameters.AddWithValue("$version", migration.Version);
            ledgerCommand.Parameters.AddWithValue("$name", migration.Name);
            ledgerCommand.Parameters.AddWithValue("$sha256", migration.Sha256);
            ledgerCommand.Parameters.AddWithValue("$applied_at_utc", UtcTimestamp.Format(DateTimeOffset.UtcNow));
            ledgerCommand.ExecuteNonQuery();
        }

        using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.Transaction = transaction;
            versionCommand.CommandText = $"PRAGMA user_version = {migration.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)};";
            versionCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void Verify(SqliteConnection connection, IReadOnlyList<StorageMigration> migrations)
    {
        if (!TableExists(connection, "schema_migrations"))
        {
            throw new InvalidOperationException("Storage migration history is missing.");
        }

        var applied = new List<(int Version, string Name, string Sha256)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT version, name, sha256 FROM schema_migrations ORDER BY version;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                applied.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        if (applied.Count == 0)
        {
            throw new InvalidOperationException("Storage migration history contains no applied migrations.");
        }

        if (applied.Count > migrations.Count)
        {
            throw new InvalidOperationException("Storage schema is newer than this application supports.");
        }

        for (var index = 0; index < applied.Count; index++)
        {
            var recorded = applied[index];
            if (recorded.Version != index + 1)
            {
                throw new InvalidOperationException("Storage migration history is not contiguous.");
            }

            var expected = migrations[index];
            if (recorded.Version != expected.Version ||
                !string.Equals(recorded.Name, expected.Name, StringComparison.Ordinal) ||
                !string.Equals(recorded.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Storage migration checksum or name mismatch at version {recorded.Version}.");
            }
        }

        if (applied.Count != migrations.Count)
        {
            throw new InvalidOperationException("Storage is behind the supported schema and requires an explicit migration.");
        }

        var schemaVersion = ReadSchemaVersion(connection);
        var latestVersion = migrations[^1].Version;
        if (schemaVersion > latestVersion)
        {
            throw new InvalidOperationException("Storage schema is newer than this application supports.");
        }

        if (schemaVersion != applied[^1].Version || schemaVersion != latestVersion)
        {
            throw new InvalidOperationException("SQLite schema version does not match verified migration history.");
        }

        if (ScalarInt64(connection, "PRAGMA foreign_keys;") != 1)
        {
            throw new InvalidOperationException("SQLite foreign-key enforcement is disabled.");
        }

        using var foreignKeyCheck = connection.CreateCommand();
        foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
        using var foreignKeyReader = foreignKeyCheck.ExecuteReader();
        if (foreignKeyReader.Read())
        {
            throw new InvalidOperationException("Storage contains a foreign-key integrity violation.");
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name);";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static int ReadSchemaVersion(SqliteConnection connection) =>
        checked((int)ScalarInt64(connection, "PRAGMA user_version;"));

    private static long ScalarInt64(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
