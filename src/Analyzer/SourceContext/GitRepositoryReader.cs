using System.Globalization;
using System.Text;

namespace IssueHarbor.Analyzer.SourceContext;

public sealed class GitRepositoryReader
{
    private const long MaximumTreeOutputBytes = 64L * 1024 * 1024;
    private const long MaximumSearchOutputBytes = 16L * 1024 * 1024;
    private const int MaximumSearchResults = 1_000;
    private const int MaximumExcerptCharacters = 4_096;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UTF8Encoding ReplacementUtf8 = new(false, false);
    private readonly GitRepositoryCatalog _catalog;
    private readonly GitProcessRunner _processRunner;

    public GitRepositoryReader(GitRepositoryCatalog catalog)
        : this(catalog, new GitProcessRunner())
    {
    }

    internal GitRepositoryReader(GitRepositoryCatalog catalog, GitProcessRunner processRunner)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<ResolvedSourceContext?> ResolveForProjectAsync(
        long redmineProjectId,
        CancellationToken cancellationToken = default)
    {
        if (redmineProjectId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(redmineProjectId));
        }

        if (!_catalog.TryGetByProjectId(redmineProjectId, out var mapping))
        {
            return null;
        }

        return await ResolveMappingAsync(mapping, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedSourceContext> ResolveRepositoryAsync(
        string repositoryKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryKey);
        if (!_catalog.TryGetByKey(repositoryKey, out var mapping))
        {
            throw new SourceContextException(SourceContextFailureCode.ConfigurationInvalid, "The repository key is not configured.");
        }

        return await ResolveMappingAsync(mapping, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GitTreeEntry>> ListTreeAsync(
        ResolvedSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = await _processRunner.RunAsync(
            context.RepositoryPath,
            ["ls-tree", "-r", "-l", "-z", "--full-tree", context.CommitOid],
            MaximumTreeOutputBytes,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, "Git tree enumeration failed.");

        var entries = new List<GitTreeEntry>();
        var bytes = result.StandardOutput;
        var recordStart = 0;
        while (recordStart < bytes.Length)
        {
            var recordEnd = Array.IndexOf(bytes, (byte)0, recordStart);
            if (recordEnd < 0)
            {
                throw InvalidGitOutput("Git tree output was not NUL terminated.");
            }

            var tab = Array.IndexOf(bytes, (byte)'\t', recordStart, recordEnd - recordStart);
            if (tab < 0)
            {
                throw InvalidGitOutput("Git tree output had an invalid record.");
            }

            var header = Encoding.ASCII.GetString(bytes, recordStart, tab - recordStart);
            var fields = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 4 || !IsObjectId(fields[2]))
            {
                throw InvalidGitOutput("Git tree output had an invalid object record.");
            }

            long? size;
            if (fields[3] == "-")
            {
                size = null;
            }
            else if (long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSize) && parsedSize >= 0)
            {
                size = parsedSize;
            }
            else
            {
                throw InvalidGitOutput("Git tree output had an invalid object size.");
            }

            string path;
            try
            {
                path = StrictUtf8.GetString(bytes, tab + 1, recordEnd - tab - 1);
            }
            catch (DecoderFallbackException)
            {
                throw InvalidGitOutput("Git tree output contained a path that was not valid UTF-8.");
            }

            if (path.Length == 0)
            {
                throw InvalidGitOutput("Git tree output contained an empty path.");
            }

            entries.Add(new GitTreeEntry(path, fields[0], fields[1], fields[2], size, context.CommitOid));
            recordStart = recordEnd + 1;
        }

        return entries.AsReadOnly();
    }

    public async Task<byte[]> ReadBlobAsync(
        ResolvedSourceContext context,
        GitTreeEntry entry,
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entry);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "The blob byte limit must be positive.");
        }

        if (!string.Equals(entry.ResolvedCommitOid, context.CommitOid, StringComparison.Ordinal)
            || entry.ObjectType != "blob")
        {
            throw new SourceContextException(SourceContextFailureCode.ObjectTypeMismatch, "The selected tree entry is not a blob in this resolved commit.");
        }

        var typeResult = await _processRunner.RunAsync(
            context.RepositoryPath,
            ["cat-file", "-t", entry.ObjectOid],
            128,
            cancellationToken).ConfigureAwait(false);
        if (typeResult.ExitCode != 0)
        {
            throw new SourceContextException(SourceContextFailureCode.ObjectMissing, "The Git object is not locally available.");
        }

        if (!string.Equals(ReadSingleLine(typeResult.StandardOutput), "blob", StringComparison.Ordinal))
        {
            throw new SourceContextException(SourceContextFailureCode.ObjectTypeMismatch, "The selected Git object is not a blob.");
        }

        var sizeResult = await _processRunner.RunAsync(
            context.RepositoryPath,
            ["cat-file", "-s", entry.ObjectOid],
            128,
            cancellationToken).ConfigureAwait(false);
        if (sizeResult.ExitCode != 0
            || !long.TryParse(ReadSingleLine(sizeResult.StandardOutput), NumberStyles.None, CultureInfo.InvariantCulture, out var objectSize)
            || objectSize < 0)
        {
            throw new SourceContextException(SourceContextFailureCode.InvalidGitOutput, "Git returned an invalid blob size.");
        }

        if (entry.Size != objectSize)
        {
            throw InvalidGitOutput("The tree entry size did not match the Git object size.");
        }

        if (objectSize > maximumBytes || objectSize > Array.MaxLength)
        {
            throw new SourceContextException(SourceContextFailureCode.BlobTooLarge, "The Git blob exceeds the caller-provided byte limit.");
        }

        var blobResult = await _processRunner.RunAsync(
            context.RepositoryPath,
            ["cat-file", "blob", entry.ObjectOid],
            objectSize,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(blobResult, "Git blob read failed.");
        if (blobResult.StandardOutput.LongLength != objectSize)
        {
            throw InvalidGitOutput("Git returned a blob with an unexpected byte count.");
        }

        return blobResult.StandardOutput;
    }

    public async Task<IReadOnlyList<GitSearchMatch>> SearchAsync(
        ResolvedSourceContext context,
        string pattern,
        IEnumerable<string>? pathFilters,
        int resultLimit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (pattern is null || pattern.IndexOf('\0') >= 0)
        {
            throw new SourceContextException(SourceContextFailureCode.SearchInputInvalid, "The search pattern is invalid.");
        }

        if (resultLimit is < 1 or > MaximumSearchResults)
        {
            throw new SourceContextException(SourceContextFailureCode.SearchInputInvalid, "The search result limit must be between 1 and 1000.");
        }

        var filters = (pathFilters ?? []).ToArray();
        if (filters.Any(static filter => !IsSafePathFilter(filter)))
        {
            throw new SourceContextException(SourceContextFailureCode.SearchInputInvalid, "A source path filter is invalid.");
        }

        var arguments = new List<string>
        {
            "grep", "--no-color", "--full-name", "-n", "-z", "-I", "-F", "-m", resultLimit.ToString(CultureInfo.InvariantCulture),
            "-e", pattern, context.CommitOid, "--"
        };
        arguments.AddRange(filters);

        var result = await _processRunner.RunAsync(
            context.RepositoryPath,
            arguments,
            MaximumSearchOutputBytes,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return Array.Empty<GitSearchMatch>();
        }

        EnsureSuccess(result, "Git source search failed.");
        var matches = new List<GitSearchMatch>(resultLimit);
        var bytes = result.StandardOutput;
        var offset = 0;
        var revisionPathPrefix = Encoding.ASCII.GetBytes($"{context.CommitOid}:");
        while (offset < bytes.Length && matches.Count < resultLimit)
        {
            var pathEnd = Array.IndexOf(bytes, (byte)0, offset);
            if (pathEnd < 0)
            {
                throw InvalidGitOutput("Git search output had an unterminated path.");
            }

            var pathStart = offset;
            var pathLength = pathEnd - pathStart;
            if (pathLength <= revisionPathPrefix.Length
                || !bytes.AsSpan(pathStart, revisionPathPrefix.Length).SequenceEqual(revisionPathPrefix))
            {
                throw InvalidGitOutput("Git search output did not identify the resolved commit path.");
            }

            string path;
            try
            {
                path = StrictUtf8.GetString(bytes, pathStart + revisionPathPrefix.Length, pathLength - revisionPathPrefix.Length);
            }
            catch (DecoderFallbackException)
            {
                throw InvalidGitOutput("Git search output contained a path that was not valid UTF-8.");
            }

            var lineNumberStart = pathEnd + 1;
            var lineNumberEnd = Array.IndexOf(bytes, (byte)0, lineNumberStart);
            if (lineNumberEnd < 0)
            {
                throw InvalidGitOutput("Git search output had an unterminated line number.");
            }

            if (!long.TryParse(Encoding.ASCII.GetString(bytes, lineNumberStart, lineNumberEnd - lineNumberStart), NumberStyles.None, CultureInfo.InvariantCulture, out var lineNumber)
                || lineNumber <= 0)
            {
                throw InvalidGitOutput("Git search output contained an invalid line number.");
            }

            var textStart = lineNumberEnd + 1;
            var textEnd = Array.IndexOf(bytes, (byte)'\n', textStart);
            if (textEnd < 0)
            {
                throw InvalidGitOutput("Git search output had an unterminated match line.");
            }

            var fullText = ReplacementUtf8.GetString(bytes, textStart, textEnd - textStart);
            var isExcerpt = fullText.Length > MaximumExcerptCharacters;
            var excerpt = isExcerpt ? fullText[..MaximumExcerptCharacters] : fullText;
            matches.Add(new GitSearchMatch(path, lineNumber, excerpt, isExcerpt));
            offset = textEnd + 1;
        }

        return matches.AsReadOnly();
    }

    private async Task<ResolvedSourceContext> ResolveMappingAsync(
        GitRepositoryMapping mapping,
        CancellationToken cancellationToken)
    {
        var formatResult = await _processRunner.RunAsync(
            mapping.Path,
            ["rev-parse", "--show-object-format=storage"],
            128,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(formatResult, "Git object format discovery failed.");
        var objectFormat = ReadSingleLine(formatResult.StandardOutput);
        if (objectFormat is not ("sha1" or "sha256"))
        {
            throw InvalidGitOutput("Git reported an unsupported object format.");
        }

        var commitResult = await _processRunner.RunAsync(
            mapping.Path,
            ["rev-parse", "--verify", "--end-of-options", $"{mapping.Revision}^{{commit}}"],
            256,
            cancellationToken).ConfigureAwait(false);
        if (commitResult.ExitCode != 0)
        {
            throw new SourceContextException(SourceContextFailureCode.RevisionUnresolved, "The configured revision did not resolve to a commit.");
        }

        var commitOid = ReadObjectId(commitResult.StandardOutput);
        var treeResult = await _processRunner.RunAsync(
            mapping.Path,
            ["rev-parse", "--verify", "--end-of-options", $"{commitOid}^{{tree}}"],
            256,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccess(treeResult, "The resolved commit tree could not be read.");
        var rootTreeOid = ReadObjectId(treeResult.StandardOutput);

        return new ResolvedSourceContext(mapping.Key, mapping.Revision, objectFormat, commitOid, rootTreeOid, mapping.Path);
    }

    private static string ReadObjectId(byte[] output)
    {
        var objectId = ReadSingleLine(output);
        if (!IsObjectId(objectId))
        {
            throw InvalidGitOutput("Git returned an invalid object ID.");
        }

        return objectId;
    }

    private static string ReadSingleLine(byte[] output)
    {
        var value = Encoding.ASCII.GetString(output).TrimEnd('\r', '\n');
        if (value.Length == 0 || value.Contains('\n') || value.Contains('\r'))
        {
            throw InvalidGitOutput("Git returned an invalid single-line value.");
        }

        return value;
    }

    private static bool IsObjectId(string value) =>
        value.Length > 0 && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafePathFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)
            || filter.IndexOf('\0') >= 0
            || filter.Contains('\\')
            || filter.StartsWith('/')
            || filter.StartsWith(':')
            || System.IO.Path.IsPathRooted(filter))
        {
            return false;
        }

        return !filter.Split('/').Any(static segment => segment is ".." or ".");
    }

    private static void EnsureSuccess(GitProcessResult result, string safeMessage)
    {
        if (result.ExitCode != 0)
        {
            throw new SourceContextException(SourceContextFailureCode.GitCommandFailed, safeMessage);
        }
    }

    private static SourceContextException InvalidGitOutput(string safeMessage) =>
        new(SourceContextFailureCode.InvalidGitOutput, safeMessage);
}
