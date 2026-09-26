using System.Text.Json.Serialization;

namespace IssueHarbor.Analyzer.SourceContext;

public enum SourceContextFailureCode
{
    ConfigurationInvalid,
    RevisionUnresolved,
    GitCommandFailed,
    GitProcessTimedOut,
    InvalidGitOutput,
    ObjectMissing,
    ObjectTypeMismatch,
    BlobTooLarge,
    SearchInputInvalid
}

public sealed class SourceContextException : Exception
{
    public SourceContextException(SourceContextFailureCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public SourceContextFailureCode Code { get; }
}

public sealed class ResolvedSourceContext
{
    internal ResolvedSourceContext(
        string repositoryKey,
        string revisionSpec,
        string objectFormat,
        string commitOid,
        string rootTreeOid,
        string repositoryPath)
    {
        RepositoryKey = repositoryKey;
        RevisionSpec = revisionSpec;
        ObjectFormat = objectFormat;
        CommitOid = commitOid;
        RootTreeOid = rootTreeOid;
        RepositoryPath = repositoryPath;
    }

    [JsonPropertyName("repository_key")]
    public string RepositoryKey { get; }

    [JsonPropertyName("revision_spec")]
    public string RevisionSpec { get; }

    [JsonPropertyName("object_format")]
    public string ObjectFormat { get; }

    [JsonPropertyName("commit_oid")]
    public string CommitOid { get; }

    [JsonPropertyName("root_tree_oid")]
    public string RootTreeOid { get; }

    internal string RepositoryPath { get; }
}

public sealed class GitTreeEntry
{
    internal GitTreeEntry(
        string path,
        string mode,
        string objectType,
        string objectOid,
        long? size,
        string resolvedCommitOid)
    {
        Path = path;
        Mode = mode;
        ObjectType = objectType;
        ObjectOid = objectOid;
        Size = size;
        ResolvedCommitOid = resolvedCommitOid;
    }

    [JsonPropertyName("path")]
    public string Path { get; }

    [JsonPropertyName("mode")]
    public string Mode { get; }

    [JsonPropertyName("object_type")]
    public string ObjectType { get; }

    [JsonPropertyName("object_oid")]
    public string ObjectOid { get; }

    [JsonPropertyName("size")]
    public long? Size { get; }

    internal string ResolvedCommitOid { get; }
}

public sealed record GitSearchMatch(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("line_number")] long LineNumber,
    [property: JsonPropertyName("matched_text")] string MatchedText,
    [property: JsonPropertyName("is_excerpt")] bool IsExcerpt);
