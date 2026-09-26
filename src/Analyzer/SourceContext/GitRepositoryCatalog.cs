using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace IssueHarbor.Analyzer.SourceContext;

public sealed class GitRepositoryMappingOptions
{
    public string? Key { get; set; }

    [ConfigurationKeyName("redmine_project_ids")]
    public long[]? RedmineProjectIds { get; set; }

    public string? Path { get; set; }

    public string? Revision { get; set; } = "HEAD";
}

internal sealed record GitRepositoryMapping(
    string Key,
    IReadOnlyList<long> RedmineProjectIds,
    string Path,
    string Revision);

public sealed class GitRepositoryCatalog
{
    private static readonly Regex KeyPattern = new("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);
    private readonly IReadOnlyDictionary<string, GitRepositoryMapping> _mappingsByKey;
    private readonly IReadOnlyDictionary<long, GitRepositoryMapping> _mappingsByProjectId;

    public GitRepositoryCatalog(IEnumerable<GitRepositoryMappingOptions>? mappings)
    {
        var byKey = new Dictionary<string, GitRepositoryMapping>(StringComparer.Ordinal);
        var byProjectId = new Dictionary<long, GitRepositoryMapping>();

        foreach (var options in mappings ?? [])
        {
            if (options is null || string.IsNullOrWhiteSpace(options.Key) || !KeyPattern.IsMatch(options.Key))
            {
                throw InvalidConfiguration("Each repository mapping must have a valid key.");
            }

            if (byKey.ContainsKey(options.Key))
            {
                throw InvalidConfiguration("Repository keys must be unique.");
            }

            if (string.IsNullOrWhiteSpace(options.Path)
                || options.Path.IndexOf('\0') >= 0
                || !System.IO.Path.IsPathFullyQualified(options.Path))
            {
                throw InvalidConfiguration("Each repository path must be an absolute path.");
            }

            var projectIds = options.RedmineProjectIds;
            if (projectIds is null || projectIds.Length == 0 || projectIds.Any(static id => id <= 0))
            {
                throw InvalidConfiguration("Each repository mapping must contain positive Redmine project IDs.");
            }

            if (projectIds.Distinct().Count() != projectIds.Length)
            {
                throw InvalidConfiguration("Redmine project IDs must not repeat within a mapping.");
            }

            var revision = string.IsNullOrWhiteSpace(options.Revision) ? "HEAD" : options.Revision;
            if (revision.IndexOf('\0') >= 0)
            {
                throw InvalidConfiguration("The configured revision is invalid.");
            }

            var mapping = new GitRepositoryMapping(
                options.Key,
                Array.AsReadOnly(projectIds.ToArray()),
                System.IO.Path.GetFullPath(options.Path),
                revision);
            byKey.Add(mapping.Key, mapping);

            foreach (var projectId in projectIds)
            {
                if (!byProjectId.TryAdd(projectId, mapping))
                {
                    throw InvalidConfiguration("A Redmine project may map to only one Git repository.");
                }
            }
        }

        _mappingsByKey = byKey;
        _mappingsByProjectId = byProjectId;
    }

    public IReadOnlyCollection<string> RepositoryKeys => _mappingsByKey.Keys.Order(StringComparer.Ordinal).ToArray();

    internal bool TryGetByKey(string key, out GitRepositoryMapping mapping) => _mappingsByKey.TryGetValue(key, out mapping!);

    internal bool TryGetByProjectId(long projectId, out GitRepositoryMapping mapping) =>
        _mappingsByProjectId.TryGetValue(projectId, out mapping!);

    private static SourceContextException InvalidConfiguration(string message) =>
        new(SourceContextFailureCode.ConfigurationInvalid, message);
}
