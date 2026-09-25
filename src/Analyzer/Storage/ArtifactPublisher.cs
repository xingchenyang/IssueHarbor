using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json.Validator;
using Microsoft.Data.Sqlite;

namespace IssueHarbor.Analyzer.Storage;

public sealed record ArtifactCatalogEntry(string RelativePath, string Sha256, long SizeBytes);

public sealed record PublishedSnapshot(Guid SnapshotId, ArtifactCatalogEntry Artifact);

public sealed record PublishedRun(Guid RunId, ArtifactCatalogEntry Result, ArtifactCatalogEntry Report);

public sealed class ArtifactPublisher
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly SqliteConnectionFactory _connections;
    private readonly Lazy<JsonSchema> _resultSchema;

    public ArtifactPublisher(SqliteConnectionFactory connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _resultSchema = new Lazy<JsonSchema>(LoadResultSchema);
    }

    public PublishedSnapshot PublishSnapshot(
        Guid snapshotId,
        long issueId,
        DateTimeOffset createdAtUtc,
        byte[] snapshotJson)
    {
        ArgumentNullException.ThrowIfNull(snapshotJson);
        var id = StorageIds.ToCanonicalLowercase(snapshotId);
        ValidateSnapshotJson(snapshotJson);

        var relativePath = $"snapshots/{id}/snapshot.json";
        var artifact = Describe(relativePath, snapshotJson);
        EnsureNotCatalogued("snapshots", "snapshot_id", id);
        PublishFile(snapshotJson, GetAbsolutePath(relativePath));

        using var connection = _connections.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO snapshots (
                snapshot_id, issue_id, created_at_utc, artifact_relative_path,
                artifact_sha256, artifact_size_bytes)
            VALUES (
                $snapshot_id, $issue_id, $created_at_utc, $artifact_relative_path,
                $artifact_sha256, $artifact_size_bytes);
            """;
        command.Parameters.AddWithValue("$snapshot_id", id);
        command.Parameters.AddWithValue("$issue_id", issueId);
        command.Parameters.AddWithValue("$created_at_utc", UtcTimestamp.Format(createdAtUtc));
        command.Parameters.AddWithValue("$artifact_relative_path", artifact.RelativePath);
        command.Parameters.AddWithValue("$artifact_sha256", artifact.Sha256);
        command.Parameters.AddWithValue("$artifact_size_bytes", artifact.SizeBytes);
        command.ExecuteNonQuery();
        transaction.Commit();

        return new PublishedSnapshot(snapshotId, artifact);
    }

    public PublishedRun PublishRun(
        Guid runId,
        Guid requestItemId,
        Guid snapshotId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        byte[] resultJson,
        byte[] reportMarkdown)
    {
        ArgumentNullException.ThrowIfNull(resultJson);
        ArgumentNullException.ThrowIfNull(reportMarkdown);
        var runIdText = StorageIds.ToCanonicalLowercase(runId);
        var requestItemIdText = StorageIds.ToCanonicalLowercase(requestItemId);
        var snapshotIdText = StorageIds.ToCanonicalLowercase(snapshotId);

        var result = ReadValidatedResult(resultJson);
        ValidateReport(reportMarkdown);

        var resultArtifact = Describe($"runs/{runIdText}/result.json", resultJson);
        var reportArtifact = Describe($"runs/{runIdText}/report.md", reportMarkdown);
        EnsureNotCatalogued("runs", "run_id", runIdText);

        var resultAbsolutePath = GetAbsolutePath(resultArtifact.RelativePath);
        var reportAbsolutePath = GetAbsolutePath(reportArtifact.RelativePath);
        if (File.Exists(resultAbsolutePath) || File.Exists(reportAbsolutePath))
        {
            throw new InvalidOperationException("An immutable Run artifact already exists for this identity.");
        }

        var resultStagingPath = WriteStagingFile(resultJson, resultAbsolutePath);
        string? reportStagingPath = null;
        try
        {
            reportStagingPath = WriteStagingFile(reportMarkdown, reportAbsolutePath);
            File.Move(resultStagingPath, resultAbsolutePath);
            resultStagingPath = string.Empty;
            File.Move(reportStagingPath, reportAbsolutePath);
            reportStagingPath = null;
        }
        finally
        {
            DeleteStagingFile(resultStagingPath);
            if (reportStagingPath is not null)
            {
                DeleteStagingFile(reportStagingPath);
            }
        }

        using var connection = _connections.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO runs (
                run_id, request_item_id, snapshot_id, started_at_utc, finished_at_utc,
                result_schema_version, change_type, implementation_complexity,
                requires_code_change, confidence, complexity_rationale,
                result_relative_path, result_sha256, result_size_bytes,
                report_relative_path, report_sha256, report_size_bytes)
            VALUES (
                $run_id, $request_item_id, $snapshot_id, $started_at_utc, $finished_at_utc,
                $result_schema_version, $change_type, $implementation_complexity,
                $requires_code_change, $confidence, $complexity_rationale,
                $result_relative_path, $result_sha256, $result_size_bytes,
                $report_relative_path, $report_sha256, $report_size_bytes);
            """;
        command.Parameters.AddWithValue("$run_id", runIdText);
        command.Parameters.AddWithValue("$request_item_id", requestItemIdText);
        command.Parameters.AddWithValue("$snapshot_id", snapshotIdText);
        command.Parameters.AddWithValue("$started_at_utc", UtcTimestamp.Format(startedAtUtc));
        command.Parameters.AddWithValue("$finished_at_utc", UtcTimestamp.Format(finishedAtUtc));
        command.Parameters.AddWithValue("$result_schema_version", result.SchemaVersion);
        command.Parameters.AddWithValue("$change_type", result.ChangeType);
        command.Parameters.AddWithValue("$implementation_complexity", result.ImplementationComplexity);
        command.Parameters.AddWithValue("$requires_code_change", result.RequiresCodeChange);
        command.Parameters.AddWithValue("$confidence", result.Confidence);
        command.Parameters.AddWithValue("$complexity_rationale", result.ComplexityRationale);
        command.Parameters.AddWithValue("$result_relative_path", resultArtifact.RelativePath);
        command.Parameters.AddWithValue("$result_sha256", resultArtifact.Sha256);
        command.Parameters.AddWithValue("$result_size_bytes", resultArtifact.SizeBytes);
        command.Parameters.AddWithValue("$report_relative_path", reportArtifact.RelativePath);
        command.Parameters.AddWithValue("$report_sha256", reportArtifact.Sha256);
        command.Parameters.AddWithValue("$report_size_bytes", reportArtifact.SizeBytes);
        command.ExecuteNonQuery();
        transaction.Commit();

        return new PublishedRun(runId, resultArtifact, reportArtifact);
    }

    private static JsonSchema LoadResultSchema()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Contracts", "analysis-result-v1.schema.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The structured-result v1 schema is missing from application content.");
        }

        return JsonSchema.FromFile(path);
    }

    private static void ValidateSnapshotJson(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ArgumentException("Snapshot JSON must not be empty.", nameof(bytes));
        }

        using var document = JsonDocument.Parse(StrictUtf8.GetString(bytes));
    }

    private RunResult ReadValidatedResult(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ArgumentException("Structured result JSON must not be empty.", nameof(bytes));
        }

        var json = StrictUtf8.GetString(bytes);
        if (!_resultSchema.Value.Validate(json))
        {
            throw new InvalidOperationException("Structured result does not satisfy schema v1.");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new RunResult(
            root.GetProperty("schema_version"u8).GetInt32(),
            root.GetProperty("change_type"u8).GetString()!,
            root.GetProperty("implementation_complexity"u8).GetString()!,
            root.GetProperty("requires_code_change"u8).GetString()!,
            root.GetProperty("confidence"u8).GetString()!,
            root.GetProperty("complexity_rationale"u8).GetString()!);
    }

    private static void ValidateReport(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ArgumentException("Run report must not be empty.", nameof(bytes));
        }

        _ = StrictUtf8.GetString(bytes);
    }

    private static ArtifactCatalogEntry Describe(string relativePath, byte[] content) => new(
        relativePath,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
        content.LongLength);

    private void EnsureNotCatalogued(string tableName, string idColumnName, string id)
    {
        using var connection = _connections.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {tableName} WHERE {idColumnName} = $id);";
        command.Parameters.AddWithValue("$id", id);
        if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1)
        {
            throw new InvalidOperationException("An immutable artifact identity is already catalogued.");
        }
    }

    private string GetAbsolutePath(string relativePath) => Path.Combine(
        _connections.DurableDataRoot,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static void PublishFile(byte[] content, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            throw new InvalidOperationException("An immutable artifact already exists at the publication path.");
        }

        var stagingPath = WriteStagingFile(content, destinationPath);
        try
        {
            File.Move(stagingPath, destinationPath);
            stagingPath = string.Empty;
        }
        finally
        {
            DeleteStagingFile(stagingPath);
        }
    }

    private static string WriteStagingFile(byte[] content, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Artifact destination has no parent directory.");
        Directory.CreateDirectory(directory);

        var stagingPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destinationPath)}.{Guid.CreateVersion7():N}.staging");

        try
        {
            using var stream = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            stream.Write(content);
            stream.Flush(flushToDisk: true);
            return stagingPath;
        }
        catch
        {
            DeleteStagingFile(stagingPath);
            throw;
        }
    }

    private static void DeleteStagingFile(string stagingPath)
    {
        if (!string.IsNullOrEmpty(stagingPath) && File.Exists(stagingPath))
        {
            File.Delete(stagingPath);
        }
    }

    private sealed record RunResult(
        int SchemaVersion,
        string ChangeType,
        string ImplementationComplexity,
        string RequiresCodeChange,
        string Confidence,
        string ComplexityRationale);
}
