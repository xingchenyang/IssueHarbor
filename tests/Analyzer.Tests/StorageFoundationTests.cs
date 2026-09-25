using System.Security.Cryptography;
using System.Text;
using IssueHarbor.Analyzer.Storage;
using Microsoft.Data.Sqlite;

namespace IssueHarbor.Analyzer.Tests;

[TestClass]
public sealed class StorageFoundationTests
{
    private const string FixedTimestamp = "2026-01-02T03:04:05.000Z";

    private string _dataRoot = null!;
    private string _migrationsDirectory = null!;
    private SqliteConnectionFactory _connections = null!;
    private StorageInitializer _initializer = null!;

    [TestInitialize]
    public void SetUp()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), $"issueharbor-storage-tests-{Guid.CreateVersion7():N}");
        _migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Storage", "Migrations");
        _connections = new SqliteConnectionFactory(_dataRoot);
        _initializer = new StorageInitializer(_connections, _migrationsDirectory);
    }

    [TestCleanup]
    public void CleanUp()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Fresh_initialization_creates_schema_version_one_and_only_approved_tables()
    {
        _initializer.InitializeOrVerify();

        using var connection = _connections.OpenConnection();
        Assert.AreEqual(1, ScalarInt32(connection, "PRAGMA user_version;"));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "schema_migrations",
                "issues",
                "requests",
                "request_issues",
                "request_items",
                "snapshots",
                "runs"
            },
            ReadUserTableNames(connection));
    }

    [TestMethod]
    public void Repeated_initialization_is_idempotent()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        var appliedAt = ScalarString(connection, "SELECT applied_at_utc FROM schema_migrations WHERE version = 1;");

        _initializer.InitializeOrVerify();

        Assert.AreEqual(1, ScalarInt32(connection, "SELECT COUNT(*) FROM schema_migrations;"));
        Assert.AreEqual(1, ScalarInt32(connection, "PRAGMA user_version;"));
        Assert.AreEqual(appliedAt, ScalarString(connection, "SELECT applied_at_utc FROM schema_migrations WHERE version = 1;"));
    }

    [TestMethod]
    public void Applied_migration_checksum_drift_is_rejected()
    {
        var copiedMigrations = CopyMigrations("migration-drift");
        var initializer = new StorageInitializer(_connections, copiedMigrations);
        initializer.InitializeOrVerify();

        File.AppendAllText(Path.Combine(copiedMigrations, "001_initial.sql"), "\n-- changed after application\n", Encoding.UTF8);

        AssertThrows<InvalidOperationException>(initializer.InitializeOrVerify);
    }

    [TestMethod]
    public void Migration_checksum_is_stable_across_line_endings()
    {
        var copiedMigrations = CopyMigrations("migration-line-endings");
        var initializer = new StorageInitializer(_connections, copiedMigrations);
        initializer.InitializeOrVerify();

        var migrationPath = Path.Combine(copiedMigrations, "001_initial.sql");
        var canonicalSql = File.ReadAllText(migrationPath)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        File.WriteAllText(migrationPath, canonicalSql.Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(false));

        initializer.InitializeOrVerify();
    }

    [TestMethod]
    public void Missing_migration_history_is_rejected()
    {
        _initializer.InitializeOrVerify();
        using (var connection = _connections.OpenConnection())
        {
            Execute(connection, "DROP TABLE schema_migrations;");
        }

        AssertThrows<InvalidOperationException>(_initializer.InitializeOrVerify);
    }

    [TestMethod]
    public void Non_contiguous_migration_files_are_rejected()
    {
        var migrationDirectory = Path.Combine(_dataRoot, "non-contiguous-migrations");
        Directory.CreateDirectory(migrationDirectory);
        File.WriteAllText(Path.Combine(migrationDirectory, "002_probe.sql"), "SELECT 1;", Encoding.UTF8);

        var initializer = new StorageInitializer(_connections, migrationDirectory);
        AssertThrows<InvalidOperationException>(initializer.InitializeOrVerify);
        Assert.IsFalse(File.Exists(_connections.DatabasePath));
    }

    [TestMethod]
    public void Schema_version_mismatch_is_rejected()
    {
        _initializer.InitializeOrVerify();
        using (var connection = _connections.OpenConnection())
        {
            Execute(connection, "PRAGMA user_version = 9;");
        }

        AssertThrows<InvalidOperationException>(_initializer.InitializeOrVerify);
    }

    [TestMethod]
    public void Existing_storage_is_not_automatically_migrated()
    {
        var firstMigrationDirectory = CopyMigrations("before-upgrade");
        new StorageInitializer(_connections, firstMigrationDirectory).InitializeOrVerify();
        File.WriteAllText(Path.Combine(firstMigrationDirectory, "002_probe.sql"), "SELECT 1;", Encoding.UTF8);

        AssertThrows<InvalidOperationException>(
            new StorageInitializer(_connections, firstMigrationDirectory).InitializeOrVerify);

        using var connection = _connections.OpenConnection();
        Assert.AreEqual(1, ScalarInt32(connection, "PRAGMA user_version;"));
        Assert.AreEqual(1, ScalarInt32(connection, "SELECT COUNT(*) FROM schema_migrations;"));
    }

    [TestMethod]
    public void Every_connection_enables_foreign_keys()
    {
        using var connection = _connections.OpenConnection();

        Assert.AreEqual(1, ScalarInt32(connection, "PRAGMA foreign_keys;"));
    }

    [TestMethod]
    public void Foreign_key_violations_are_rejected()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO request_issues (request_id, ordinal, issue_id) VALUES ($request_id, 0, 1001);";
        command.Parameters.AddWithValue("$request_id", NewId());

        AssertThrows<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    public void Invalid_request_status_is_rejected()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();

        AssertThrows<SqliteException>(() => InsertRequest(connection, status: "queued"));
    }

    [TestMethod]
    public void Invalid_request_stop_reason_combinations_are_rejected()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();

        AssertThrows<SqliteException>(() => InsertRequest(connection, stopReason: "operator_stopped"));
        AssertThrows<SqliteException>(() => InsertRequest(connection, status: "stopped"));
    }

    [TestMethod]
    public void Duplicate_request_issue_membership_is_rejected()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        var requestId = InsertRequest(connection);
        InsertIssue(connection, 1001);
        InsertMembership(connection, requestId, 0, 1001);

        AssertThrows<SqliteException>(() => InsertMembership(connection, requestId, 1, 1001));
    }

    [TestMethod]
    public void Duplicate_request_issue_ordinal_is_rejected()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        var requestId = InsertRequest(connection);
        InsertIssue(connection, 1001);
        InsertIssue(connection, 1002);
        InsertMembership(connection, requestId, 0, 1001);

        AssertThrows<SqliteException>(() => InsertMembership(connection, requestId, 0, 1002));
    }

    [TestMethod]
    public void Invalid_request_item_status_is_rejected()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        var requestId = InsertRequest(connection);
        InsertIssue(connection, 1001);
        InsertMembership(connection, requestId, 0, 1001);

        AssertThrows<SqliteException>(() => InsertRequestItem(
            connection,
            requestId,
            1001,
            status: "queued"));
    }

    [TestMethod]
    public void Invalid_scope004_result_enum_is_rejected_by_database()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        var item = SeedRequestItem(connection, 1001);

        AssertThrows<SqliteException>(() => InsertRun(connection, item, changeType: "defect"));
    }

    [TestMethod]
    public void Snapshot_publication_records_relative_path_hash_and_size()
    {
        _initializer.InitializeOrVerify();
        using (var connection = _connections.OpenConnection())
        {
            InsertIssue(connection, 1001);
        }

        var content = Encoding.UTF8.GetBytes("{\"issue_id\":1001,\"subject\":\"Synthetic issue\"}");
        var snapshotId = StorageIds.NewVersion7();
        var published = new ArtifactPublisher(_connections).PublishSnapshot(
            snapshotId,
            1001,
            new DateTimeOffset(2026, 1, 2, 5, 4, 5, TimeSpan.FromHours(2)),
            content);

        Assert.AreEqual($"snapshots/{NewId(snapshotId)}/snapshot.json", published.Artifact.RelativePath);
        Assert.IsFalse(Path.IsPathRooted(published.Artifact.RelativePath));
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), published.Artifact.Sha256);
        Assert.AreEqual(content.LongLength, published.Artifact.SizeBytes);
        Assert.IsTrue(File.Exists(PhysicalPath(published.Artifact.RelativePath)));

        using var verifyConnection = _connections.OpenConnection();
        using var command = verifyConnection.CreateCommand();
        command.CommandText = "SELECT artifact_relative_path, artifact_sha256, artifact_size_bytes, created_at_utc FROM snapshots WHERE snapshot_id = $id;";
        command.Parameters.AddWithValue("$id", NewId(snapshotId));
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(published.Artifact.RelativePath, reader.GetString(0));
        Assert.AreEqual(published.Artifact.Sha256, reader.GetString(1));
        Assert.AreEqual(content.LongLength, reader.GetInt64(2));
        Assert.AreEqual(FixedTimestamp, reader.GetString(3));
    }

    [TestMethod]
    public void Run_publication_records_validated_result_and_report_metadata()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        InsertIssue(connection, 1001);
        var snapshotId = StorageIds.NewVersion7();
        new ArtifactPublisher(_connections).PublishSnapshot(
            snapshotId,
            1001,
            DateTimeOffset.Parse(FixedTimestamp),
            Encoding.UTF8.GetBytes("{\"issue_id\":1001}"));
        var requestId = InsertRequest(connection);
        InsertMembership(connection, requestId, 0, 1001);
        var itemId = InsertRequestItem(connection, requestId, 1001, snapshotId: NewId(snapshotId));

        var result = Encoding.UTF8.GetBytes("""
            {"schema_version":1,"change_type":"bug_fix","implementation_complexity":"low","requires_code_change":"yes","confidence":"high","complexity_rationale":"A bounded change fixes the observed behavior."}
            """);
        var report = Encoding.UTF8.GetBytes("# Synthetic report\n\nThe issue is resolved by a bounded change.\n");
        var runId = StorageIds.NewVersion7();
        var published = new ArtifactPublisher(_connections).PublishRun(
            runId,
            Guid.Parse(itemId),
            snapshotId,
            DateTimeOffset.Parse(FixedTimestamp),
            DateTimeOffset.Parse(FixedTimestamp),
            result,
            report);

        Assert.AreEqual($"runs/{NewId(runId)}/result.json", published.Result.RelativePath);
        Assert.AreEqual($"runs/{NewId(runId)}/report.md", published.Report.RelativePath);
        Assert.IsFalse(Path.IsPathRooted(published.Result.RelativePath));
        Assert.IsFalse(Path.IsPathRooted(published.Report.RelativePath));
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(result)).ToLowerInvariant(), published.Result.Sha256);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(report)).ToLowerInvariant(), published.Report.Sha256);
        Assert.AreEqual(result.LongLength, published.Result.SizeBytes);
        Assert.AreEqual(report.LongLength, published.Report.SizeBytes);
        Assert.IsTrue(File.Exists(PhysicalPath(published.Result.RelativePath)));
        Assert.IsTrue(File.Exists(PhysicalPath(published.Report.RelativePath)));

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT result_relative_path, result_sha256, result_size_bytes,
                   report_relative_path, report_sha256, report_size_bytes,
                   result_schema_version, change_type, implementation_complexity
            FROM runs WHERE run_id = $run_id;
            """;
        command.Parameters.AddWithValue("$run_id", NewId(runId));
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(published.Result.RelativePath, reader.GetString(0));
        Assert.AreEqual(published.Result.Sha256, reader.GetString(1));
        Assert.AreEqual(result.LongLength, reader.GetInt64(2));
        Assert.AreEqual(published.Report.RelativePath, reader.GetString(3));
        Assert.AreEqual(published.Report.Sha256, reader.GetString(4));
        Assert.AreEqual(report.LongLength, reader.GetInt64(5));
        Assert.AreEqual(1, reader.GetInt32(6));
        Assert.AreEqual("bug_fix", reader.GetString(7));
        Assert.AreEqual("low", reader.GetString(8));
    }

    [TestMethod]
    public void Duplicate_snapshot_identity_fails_without_overwriting_published_content()
    {
        _initializer.InitializeOrVerify();
        using (var connection = _connections.OpenConnection())
        {
            InsertIssue(connection, 1001);
        }

        var publisher = new ArtifactPublisher(_connections);
        var snapshotId = StorageIds.NewVersion7();
        var original = Encoding.UTF8.GetBytes("{\"value\":1}");
        var path = publisher.PublishSnapshot(snapshotId, 1001, DateTimeOffset.Parse(FixedTimestamp), original).Artifact.RelativePath;

        AssertThrows<InvalidOperationException>(() => publisher.PublishSnapshot(
            snapshotId,
            1001,
            DateTimeOffset.Parse(FixedTimestamp),
            Encoding.UTF8.GetBytes("{\"value\":2}")));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(PhysicalPath(path)));
    }

    [TestMethod]
    public void Duplicate_run_identity_fails_without_overwriting_published_artifacts()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        InsertIssue(connection, 1001);
        var snapshotId = StorageIds.NewVersion7();
        var publisher = new ArtifactPublisher(_connections);
        publisher.PublishSnapshot(snapshotId, 1001, DateTimeOffset.Parse(FixedTimestamp), Encoding.UTF8.GetBytes("{\"issue_id\":1001}"));
        var requestId = InsertRequest(connection);
        InsertMembership(connection, requestId, 0, 1001);
        var itemId = InsertRequestItem(connection, requestId, 1001, snapshotId: NewId(snapshotId));
        var runId = StorageIds.NewVersion7();
        var result = ValidResultJson();
        var report = Encoding.UTF8.GetBytes("# Synthetic report\n");
        var published = publisher.PublishRun(
            runId,
            Guid.Parse(itemId),
            snapshotId,
            DateTimeOffset.Parse(FixedTimestamp),
            DateTimeOffset.Parse(FixedTimestamp),
            result,
            report);

        AssertThrows<InvalidOperationException>(() => publisher.PublishRun(
            runId,
            Guid.Parse(itemId),
            snapshotId,
            DateTimeOffset.Parse(FixedTimestamp),
            DateTimeOffset.Parse(FixedTimestamp),
            Encoding.UTF8.GetBytes("{}"),
            Encoding.UTF8.GetBytes("# changed")));
        CollectionAssert.AreEqual(result, File.ReadAllBytes(PhysicalPath(published.Result.RelativePath)));
        CollectionAssert.AreEqual(report, File.ReadAllBytes(PhysicalPath(published.Report.RelativePath)));
    }

    [TestMethod]
    public void Invalid_result_is_rejected_before_artifact_publication()
    {
        _initializer.InitializeOrVerify();
        var publisher = new ArtifactPublisher(_connections);
        var runId = StorageIds.NewVersion7();
        var runDirectory = Path.Combine(_dataRoot, "runs", NewId(runId));

        AssertThrows<InvalidOperationException>(() => publisher.PublishRun(
            runId,
            StorageIds.NewVersion7(),
            StorageIds.NewVersion7(),
            DateTimeOffset.Parse(FixedTimestamp),
            DateTimeOffset.Parse(FixedTimestamp),
            Encoding.UTF8.GetBytes("{}"),
            Encoding.UTF8.GetBytes("# Synthetic report")));

        Assert.IsFalse(Directory.Exists(runDirectory));
    }

    [TestMethod]
    public void Catalog_registration_failure_leaves_orphan_artifact_and_no_database_row()
    {
        _initializer.InitializeOrVerify();
        var snapshotId = StorageIds.NewVersion7();
        var content = Encoding.UTF8.GetBytes("{\"orphan\":true}");
        var relativePath = $"snapshots/{NewId(snapshotId)}/snapshot.json";

        AssertThrows<SqliteException>(() => new ArtifactPublisher(_connections).PublishSnapshot(
            snapshotId,
            9001,
            DateTimeOffset.Parse(FixedTimestamp),
            content));

        Assert.IsTrue(File.Exists(PhysicalPath(relativePath)));
        CollectionAssert.AreEqual(content, File.ReadAllBytes(PhysicalPath(relativePath)));
        using var connection = _connections.OpenConnection();
        Assert.AreEqual(0, ScalarInt32(connection, "SELECT COUNT(*) FROM snapshots WHERE snapshot_id = $id;", ("$id", NewId(snapshotId))));
    }

    [TestMethod]
    public void Database_rejects_absolute_artifact_paths()
    {
        _initializer.InitializeOrVerify();
        using var connection = _connections.OpenConnection();
        InsertIssue(connection, 1001);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO snapshots (
                snapshot_id, issue_id, created_at_utc, artifact_relative_path,
                artifact_sha256, artifact_size_bytes)
            VALUES ($snapshot_id, 1001, $created_at_utc, $path, $sha256, 1);
            """;
        command.Parameters.AddWithValue("$snapshot_id", NewId());
        command.Parameters.AddWithValue("$created_at_utc", FixedTimestamp);
        command.Parameters.AddWithValue("$path", "C:\\private\\host\\snapshot.json");
        command.Parameters.AddWithValue("$sha256", new string('a', 64));

        AssertThrows<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    public void Utc_timestamps_are_normalized_to_rfc3339_z_text()
    {
        var input = new DateTimeOffset(2026, 1, 2, 5, 4, 5, TimeSpan.FromHours(2));

        Assert.AreEqual(FixedTimestamp, UtcTimestamp.Format(input));
    }

    [TestMethod]
    public void Storage_ids_are_uuid_version_seven_and_lowercase()
    {
        var id = StorageIds.NewVersion7();

        Assert.IsTrue(Guid.TryParseExact(StorageIds.ToCanonicalLowercase(id), "D", out _));
        Assert.AreEqual('7', StorageIds.ToCanonicalLowercase(id)[14]);
        Assert.AreEqual(StorageIds.ToCanonicalLowercase(id), StorageIds.ToCanonicalLowercase(id).ToLowerInvariant());
    }

    private string CopyMigrations(string directoryName)
    {
        var destination = Path.Combine(_dataRoot, directoryName);
        Directory.CreateDirectory(destination);
        foreach (var migration in Directory.EnumerateFiles(_migrationsDirectory, "*.sql"))
        {
            File.Copy(migration, Path.Combine(destination, Path.GetFileName(migration)));
        }

        return destination;
    }

    private string PhysicalPath(string relativePath) => Path.Combine(
        _dataRoot,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string InsertRequest(SqliteConnection connection, string status = "running", string? stopReason = null)
    {
        var requestId = NewId();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO requests (request_id, status, stop_reason, created_at_utc) VALUES ($id, $status, $reason, $created_at);";
        command.Parameters.AddWithValue("$id", requestId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$reason", (object?)stopReason ?? DBNull.Value);
        command.Parameters.AddWithValue("$created_at", FixedTimestamp);
        command.ExecuteNonQuery();
        return requestId;
    }

    private static void InsertIssue(SqliteConnection connection, long issueId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO issues (issue_id, first_seen_at_utc) VALUES ($id, $first_seen);";
        command.Parameters.AddWithValue("$id", issueId);
        command.Parameters.AddWithValue("$first_seen", FixedTimestamp);
        command.ExecuteNonQuery();
    }

    private static void InsertMembership(SqliteConnection connection, string requestId, int ordinal, long issueId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO request_issues (request_id, ordinal, issue_id) VALUES ($request, $ordinal, $issue);";
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$ordinal", ordinal);
        command.Parameters.AddWithValue("$issue", issueId);
        command.ExecuteNonQuery();
    }

    private static string InsertRequestItem(
        SqliteConnection connection,
        string requestId,
        long issueId,
        string status = "running",
        string? snapshotId = null)
    {
        var itemId = NewId();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO request_items (
                request_item_id, request_id, issue_id, profile_key, profile_ordinal,
                status, reason_code, snapshot_id)
            VALUES ($item, $request, $issue, 'default', 0, $status, NULL, $snapshot);
            """;
        command.Parameters.AddWithValue("$item", itemId);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$issue", issueId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$snapshot", (object?)snapshotId ?? DBNull.Value);
        command.ExecuteNonQuery();
        return itemId;
    }

    private static RequestItemFixture SeedRequestItem(SqliteConnection connection, long issueId)
    {
        var requestId = InsertRequest(connection);
        InsertIssue(connection, issueId);
        InsertMembership(connection, requestId, 0, issueId);

        var snapshotId = StorageIds.NewVersion7();
        var snapshotIdText = NewId(snapshotId);
        using (var snapshotCommand = connection.CreateCommand())
        {
            snapshotCommand.CommandText = """
                INSERT INTO snapshots (
                    snapshot_id, issue_id, created_at_utc, artifact_relative_path,
                    artifact_sha256, artifact_size_bytes)
                VALUES ($id, $issue, $created, $path, $sha256, 1);
                """;
            snapshotCommand.Parameters.AddWithValue("$id", snapshotIdText);
            snapshotCommand.Parameters.AddWithValue("$issue", issueId);
            snapshotCommand.Parameters.AddWithValue("$created", FixedTimestamp);
            snapshotCommand.Parameters.AddWithValue("$path", $"snapshots/{snapshotIdText}/snapshot.json");
            snapshotCommand.Parameters.AddWithValue("$sha256", new string('a', 64));
            snapshotCommand.ExecuteNonQuery();
        }

        var itemId = InsertRequestItem(connection, requestId, issueId, snapshotId: snapshotIdText);
        return new RequestItemFixture(Guid.Parse(requestId), Guid.Parse(itemId), snapshotId);
    }

    private static void InsertRun(SqliteConnection connection, RequestItemFixture item, string changeType)
    {
        var runId = NewId();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (
                run_id, request_item_id, snapshot_id, started_at_utc, finished_at_utc,
                result_schema_version, change_type, implementation_complexity,
                requires_code_change, confidence, complexity_rationale,
                result_relative_path, result_sha256, result_size_bytes,
                report_relative_path, report_sha256, report_size_bytes)
            VALUES (
                $run, $item, $snapshot, $started, $finished,
                1, $change, 'low', 'yes', 'high', 'Synthetic rationale',
                'runs/test/result.json', $result_hash, 1,
                'runs/test/report.md', $report_hash, 1);
            """;
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$item", StorageIds.ToCanonicalLowercase(item.RequestItemId));
        command.Parameters.AddWithValue("$snapshot", StorageIds.ToCanonicalLowercase(item.SnapshotId));
        command.Parameters.AddWithValue("$started", FixedTimestamp);
        command.Parameters.AddWithValue("$finished", FixedTimestamp);
        command.Parameters.AddWithValue("$change", changeType);
        command.Parameters.AddWithValue("$result_hash", new string('a', 64));
        command.Parameters.AddWithValue("$report_hash", new string('b', 64));
        command.ExecuteNonQuery();
    }

    private static byte[] ValidResultJson() => Encoding.UTF8.GetBytes("""
        {"schema_version":1,"change_type":"bug_fix","implementation_complexity":"low","requires_code_change":"yes","confidence":"high","complexity_rationale":"A bounded code change is required."}
        """);

    private static string NewId() => StorageIds.ToCanonicalLowercase(StorageIds.NewVersion7());

    private static string NewId(Guid id) => StorageIds.ToCanonicalLowercase(id);

    private static int ScalarInt32(SqliteConnection connection, string sql, (string Name, object Value)? parameter = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameter is { } value)
        {
            command.Parameters.AddWithValue(value.Name, value.Value);
        }

        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ScalarString(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    private static string[] ReadUserTableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }

    private static void AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
    }
    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed record RequestItemFixture(Guid RequestId, Guid RequestItemId, Guid SnapshotId);
}
