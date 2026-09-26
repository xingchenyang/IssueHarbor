using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IssueHarbor.Analyzer.SourceContext;
using Microsoft.Extensions.Configuration;

namespace IssueHarbor.Analyzer.Tests.SourceContext;

[TestClass]
public sealed class GitRepositoryReaderTests
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private string _testRoot = null!;
    private string _workingRepository = null!;

    [TestInitialize]
    public async Task SetUp()
    {
        _testRoot = Path.Combine(FindRepositoryRoot(), ".work", $"source-context-tests-{Guid.NewGuid():N}");
        _workingRepository = Path.Combine(_testRoot, "working-repository");
        Directory.CreateDirectory(_testRoot);
        Directory.CreateDirectory(_workingRepository);

        await RunGitAsync(_testRoot, "init", "--initial-branch=main", _workingRepository);
        await RunGitAsync(_workingRepository, "config", "user.name", "IssueHarbor Synthetic Tests");
        await RunGitAsync(_workingRepository, "config", "user.email", "synthetic@example.invalid");
        Directory.CreateDirectory(Path.Combine(_workingRepository, "src"));
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "src", "anchor.txt"), "committed marker A\n", Utf8WithoutBom);
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "readme.txt"), "Synthetic repository fixture\n", Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "--all");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic initial source");
    }

    [TestCleanup]
    public void CleanUp()
    {
        if (!Directory.Exists(_testRoot))
        {
            return;
        }

        var workRoot = Path.GetFullPath(Path.Combine(FindRepositoryRoot(), ".work")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(_testRoot);
        if (!target.StartsWith(workRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Synthetic source test cleanup escaped the repository work directory.");
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(target, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(30 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(30 * (attempt + 1));
            }
        }
    }

    [TestMethod]
    public async Task Catalog_accepts_multiple_project_ids_and_unmapped_projects_are_absent()
    {
        var catalog = new GitRepositoryCatalog(
        [
            new GitRepositoryMappingOptions
            {
                Key = "main-source",
                RedmineProjectIds = [42, 43],
                Path = _workingRepository
            }
        ]);
        var reader = new GitRepositoryReader(catalog);

        Assert.AreEqual("main-source", catalog.RepositoryKeys.Single());
        Assert.AreEqual("main-source", (await reader.ResolveForProjectAsync(43))!.RepositoryKey);
        Assert.IsNull(await reader.ResolveForProjectAsync(999));
        Assert.AreEqual(0, new GitRepositoryCatalog(null).RepositoryKeys.Count);
    }

    [TestMethod]
    public void Catalog_rejects_duplicate_keys_and_project_mappings()
    {
        var duplicateKeys = new[]
        {
            Mapping("duplicate", _workingRepository, [1]),
            Mapping("duplicate", _workingRepository, [2])
        };
        var duplicateProjects = new[]
        {
            Mapping("first", _workingRepository, [7]),
            Mapping("second", _workingRepository, [7])
        };

        Assert.AreEqual(SourceContextFailureCode.ConfigurationInvalid,
            AssertThrows<SourceContextException>(() => new GitRepositoryCatalog(duplicateKeys)).Code);
        Assert.AreEqual(SourceContextFailureCode.ConfigurationInvalid,
            AssertThrows<SourceContextException>(() => new GitRepositoryCatalog(duplicateProjects)).Code);
        Assert.AreEqual(SourceContextFailureCode.ConfigurationInvalid,
            AssertThrows<SourceContextException>(() => new GitRepositoryCatalog([Mapping("bad", _workingRepository, [1, 1])])).Code);
    }

    [TestMethod]
    public void Catalog_rejects_relative_paths_invalid_keys_and_nonpositive_project_ids()
    {
        Assert.AreEqual(SourceContextFailureCode.ConfigurationInvalid,
            AssertThrows<SourceContextException>(() => new GitRepositoryCatalog([Mapping("relative", ".", [1])])).Code);
        Assert.AreEqual(SourceContextFailureCode.ConfigurationInvalid,
            AssertThrows<SourceContextException>(() => new GitRepositoryCatalog([Mapping("Bad Key", _workingRepository, [1])])).Code);
        Assert.AreEqual(SourceContextFailureCode.ConfigurationInvalid,
            AssertThrows<SourceContextException>(() => new GitRepositoryCatalog([Mapping("zero", _workingRepository, [0])])).Code);
    }

    [TestMethod]
    public void Runtime_configuration_binds_contract_mapping_names_and_defaults_revision()
    {
        var values = new Dictionary<string, string?>
        {
            ["IssueHarbor:SourceRepositories:0:key"] = "configured-source",
            ["IssueHarbor:SourceRepositories:0:redmine_project_ids:0"] = "42",
            ["IssueHarbor:SourceRepositories:0:path"] = _workingRepository
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var mappings = configuration.GetSection("IssueHarbor:SourceRepositories").Get<List<GitRepositoryMappingOptions>>();
        var catalog = new GitRepositoryCatalog(mappings);

        Assert.AreEqual("configured-source", catalog.RepositoryKeys.Single());
        Assert.IsTrue(catalog.TryGetByProjectId(42, out var mapping));
        Assert.AreEqual("HEAD", mapping.Revision);
    }

    [TestMethod]
    public async Task Revision_head_ref_and_full_oid_resolve_to_same_commit_and_safe_provenance()
    {
        var headContext = await CreateReader(_workingRepository).ResolveForProjectAsync(42);
        var refContext = await CreateReader(_workingRepository, "refs/heads/main").ResolveForProjectAsync(42);
        var oidContext = await CreateReader(_workingRepository, headContext!.CommitOid).ResolveForProjectAsync(42);

        Assert.AreEqual(headContext.CommitOid, refContext!.CommitOid);
        Assert.AreEqual(headContext.CommitOid, oidContext!.CommitOid);
        Assert.AreEqual("HEAD", headContext.RevisionSpec);
        Assert.AreEqual("sha1", headContext.ObjectFormat);
        Assert.IsTrue(headContext.RootTreeOid.Length > 0);

        await RunGitAsync(_workingRepository, "remote", "add", "synthetic-origin", "https://example.invalid/source.git");
        var json = JsonSerializer.Serialize(headContext);
        Assert.IsFalse(json.Contains(_workingRepository, StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("example.invalid", StringComparison.Ordinal));
        StringAssert.Contains(json, "\"repository_key\":\"synthetic\"");
    }

    [TestMethod]
    public async Task Non_commit_revision_fails_deterministically()
    {
        var reader = CreateReader(_workingRepository, "refs/heads/main^{tree}");
        var exception = await AssertThrowsAsync<SourceContextException>(
            async () => await reader.ResolveForProjectAsync(42));

        Assert.AreEqual(SourceContextFailureCode.RevisionUnresolved, exception.Code);
        Assert.IsFalse(exception.Message.Contains(_workingRepository, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Tree_parser_preserves_unusual_names_and_blob_reader_returns_exact_committed_bytes()
    {
        const string unusualPath = "directory with spaces/quote ' café [one].txt";
        var expected = Encoding.UTF8.GetBytes("Synthetic unicode content\n");
        Directory.CreateDirectory(Path.Combine(_workingRepository, "directory with spaces"));
        await File.WriteAllBytesAsync(Path.Combine(_workingRepository, unusualPath), expected);
        await RunGitAsync(_workingRepository, "add", "--all");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic unusual path");

        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;
        var entry = (await reader.ListTreeAsync(context)).Single(item => item.Path == unusualPath);
        var bytes = await reader.ReadBlobAsync(context, entry, 1024);

        CollectionAssert.AreEqual(expected, bytes);
        Assert.AreEqual(expected.LongLength, entry.Size);
        Assert.AreEqual("blob", entry.ObjectType);
    }

    [TestMethod]
    public async Task Blob_reader_enforces_caller_byte_limit_and_rejects_gitlinks()
    {
        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;
        var entry = (await reader.ListTreeAsync(context)).Single(item => item.Path == "readme.txt");

        var tooSmall = await AssertThrowsAsync<SourceContextException>(
            async () => await reader.ReadBlobAsync(context, entry, entry.Size!.Value - 1));
        Assert.AreEqual(SourceContextFailureCode.BlobTooLarge, tooSmall.Code);
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            async () => await reader.ReadBlobAsync(context, entry, 0));
    }

    [TestMethod]
    public async Task Dirty_staged_and_untracked_working_tree_content_does_not_affect_tree_blob_or_search()
    {
        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;

        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "src", "anchor.txt"), "staged dirty marker\n", Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "src/anchor.txt");
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "untracked-private.txt"), "untracked marker\n", Utf8WithoutBom);

        var entries = await reader.ListTreeAsync(context);
        Assert.IsFalse(entries.Any(item => item.Path == "untracked-private.txt"));
        var anchor = entries.Single(item => item.Path == "src/anchor.txt");
        var committedBytes = await reader.ReadBlobAsync(context, anchor, 1024);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("committed marker A\n"), committedBytes);
        Assert.AreEqual(0, (await reader.SearchAsync(context, "staged dirty marker", null, 10)).Count);
        Assert.AreEqual(0, (await reader.SearchAsync(context, "untracked marker", null, 10)).Count);
        Assert.AreEqual(1, (await reader.SearchAsync(context, "committed marker A", null, 10)).Count);
    }

    [TestMethod]
    public async Task Resolved_context_remains_frozen_after_mapped_branch_moves()
    {
        var reader = CreateReader(_workingRepository, "refs/heads/main");
        var frozen = (await reader.ResolveForProjectAsync(42))!;

        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "src", "anchor.txt"), "committed marker B\n", Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "src/anchor.txt");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic moved ref");

        Assert.AreNotEqual(frozen.CommitOid, (await reader.ResolveForProjectAsync(42))!.CommitOid);
        Assert.AreEqual(1, (await reader.SearchAsync(frozen, "committed marker A", null, 10)).Count);
        Assert.AreEqual(0, (await reader.SearchAsync(frozen, "committed marker B", null, 10)).Count);
        var oldEntry = (await reader.ListTreeAsync(frozen)).Single(item => item.Path == "src/anchor.txt");
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("committed marker A\n"), await reader.ReadBlobAsync(frozen, oldEntry, 1024));
    }

    [TestMethod]
    public async Task Bare_repository_resolves_lists_reads_and_searches_without_a_working_tree()
    {
        var barePath = Path.Combine(_testRoot, "bare-mirror.git");
        var commitOid = (await RunGitAsync(_workingRepository, "rev-parse", "HEAD")).StandardOutput.Trim();
        await RunGitAsync(_testRoot, "init", "--bare", "--initial-branch=main", barePath);
        var sourceObjects = Path.Combine(_workingRepository, ".git", "objects");
        var bareObjects = Path.Combine(barePath, "objects");
        foreach (var sourceObject in Directory.EnumerateFiles(sourceObjects, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceObjects, sourceObject);
            var destination = Path.Combine(bareObjects, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(sourceObject, destination);
        }

        await RunGitAsync(_testRoot, "--git-dir", barePath, "update-ref", "refs/heads/main", commitOid);
        Assert.IsFalse(Directory.Exists(Path.Combine(barePath, "src")));

        var reader = CreateReader(barePath, "refs/heads/main");
        var context = (await reader.ResolveForProjectAsync(42))!;
        var entries = await reader.ListTreeAsync(context);
        var anchor = entries.Single(item => item.Path == "src/anchor.txt");

        Assert.AreEqual(2, entries.Count);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("committed marker A\n"), await reader.ReadBlobAsync(context, anchor, 1024));
        Assert.AreEqual(1, (await reader.SearchAsync(context, "committed marker A", null, 10)).Count);
    }

    [TestMethod]
    public async Task Symlinks_are_blob_facts_and_gitlinks_are_not_traversed()
    {
        const string symlinkTargetText = "Synthetic symlink target text";
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "symlink-source.txt"), symlinkTargetText, Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "symlink-source.txt");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic symlink source");
        var targetBlobOid = (await RunGitAsync(_workingRepository, "rev-parse", "HEAD:symlink-source.txt")).StandardOutput.Trim();
        var submoduleCommitOid = (await RunGitAsync(_workingRepository, "rev-parse", "HEAD")).StandardOutput.Trim();
        await RunGitAsync(_workingRepository, "update-index", "--add", "--cacheinfo", $"120000,{targetBlobOid},synthetic-link");
        await RunGitAsync(_workingRepository, "update-index", "--add", "--cacheinfo", $"160000,{submoduleCommitOid},synthetic-submodule");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic link and gitlink");

        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;
        var entries = await reader.ListTreeAsync(context);
        var link = entries.Single(item => item.Path == "synthetic-link");
        var gitlink = entries.Single(item => item.Path == "synthetic-submodule");

        Assert.AreEqual("120000", link.Mode);
        Assert.AreEqual("blob", link.ObjectType);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(symlinkTargetText), await reader.ReadBlobAsync(context, link, 1024));
        Assert.AreEqual("160000", gitlink.Mode);
        Assert.AreEqual("commit", gitlink.ObjectType);
        Assert.IsNull(gitlink.Size);
        Assert.IsFalse(entries.Any(item => item.Path.StartsWith("synthetic-submodule/", StringComparison.Ordinal)));
        var exception = await AssertThrowsAsync<SourceContextException>(
            async () => await reader.ReadBlobAsync(context, gitlink, 1024));
        Assert.AreEqual(SourceContextFailureCode.ObjectTypeMismatch, exception.Code);
    }

    [TestMethod]
    public async Task Search_is_commit_scoped_filtered_and_bounded_with_factual_excerpts()
    {
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "matches.txt"), "needle one\nneedle two\nneedle three\n", Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "--all");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic search matches");
        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;

        var matches = await reader.SearchAsync(context, "needle", ["matches.txt"], 2);
        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual("matches.txt", matches[0].Path);
        Assert.AreEqual(1, matches[0].LineNumber);
        Assert.AreEqual("needle one", matches[0].MatchedText);
        Assert.IsFalse(matches[0].IsExcerpt);
        Assert.AreEqual(0, (await reader.SearchAsync(context, "needle", ["readme.txt"], 10)).Count);
        await AssertThrowsAsync<SourceContextException>(
            async () => await reader.SearchAsync(context, "needle", null, 1_001));
        await AssertThrowsAsync<SourceContextException>(
            async () => await reader.SearchAsync(context, "needle", ["../outside"], 10));
    }

    [TestMethod]
    public async Task Search_returns_a_bounded_exact_excerpt_for_a_long_matching_line()
    {
        var longLine = $"prefix {new string('x', 5_000)} marker suffix";
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "long-line.txt"), longLine, Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "long-line.txt");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic long line");
        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;

        var match = (await reader.SearchAsync(context, "marker", ["long-line.txt"], 1)).Single();
        Assert.AreEqual(4_096, match.MatchedText.Length);
        Assert.IsTrue(match.IsExcerpt);
        Assert.AreEqual(longLine[..4_096], match.MatchedText);
    }

    [TestMethod]
    public async Task Sha256_repository_object_format_is_retained_without_assuming_sha1_length()
    {
        var sha256Path = Path.Combine(_testRoot, "sha256-repository");
        Directory.CreateDirectory(sha256Path);
        await RunGitAsync(_testRoot, "init", "--object-format=sha256", "--initial-branch=main", sha256Path);
        await RunGitAsync(sha256Path, "config", "user.name", "IssueHarbor Synthetic Tests");
        await RunGitAsync(sha256Path, "config", "user.email", "synthetic@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(sha256Path, "source.txt"), "sha256 synthetic source\n", Utf8WithoutBom);
        await RunGitAsync(sha256Path, "add", "source.txt");
        await RunGitAsync(sha256Path, "commit", "-m", "synthetic sha256 source");

        var context = (await CreateReader(sha256Path).ResolveForProjectAsync(42))!;
        Assert.AreEqual("sha256", context.ObjectFormat);
        Assert.AreEqual(64, context.CommitOid.Length);
        Assert.AreEqual(context.RootTreeOid.Length, context.CommitOid.Length);
    }

    [TestMethod]
    public async Task Replacement_refs_do_not_change_resolved_commit_tree_or_blob_content()
    {
        var commitA = (await RunGitAsync(_workingRepository, "rev-parse", "HEAD")).StandardOutput.Trim();
        var reader = CreateReader(_workingRepository, commitA);
        var frozen = (await reader.ResolveForProjectAsync(42))!;

        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "src", "anchor.txt"), "replacement commit content\n", Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "src/anchor.txt");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic replacement target");
        var commitB = (await RunGitAsync(_workingRepository, "rev-parse", "HEAD")).StandardOutput.Trim();
        await RunGitAsync(_workingRepository, "replace", commitA, commitB);

        Assert.AreEqual(commitA, frozen.CommitOid);
        var entry = (await reader.ListTreeAsync(frozen)).Single(item => item.Path == "src/anchor.txt");
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("committed marker A\n"), await reader.ReadBlobAsync(frozen, entry, 1024));
    }

    [TestMethod]
    public async Task Missing_local_blob_fails_without_remote_retrieval()
    {
        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;
        var entry = (await reader.ListTreeAsync(context)).Single(item => item.Path == "readme.txt");
        var looseObjectPath = Path.Combine(_workingRepository, ".git", "objects", entry.ObjectOid[..2], entry.ObjectOid[2..]);
        Assert.IsTrue(File.Exists(looseObjectPath));
        File.SetAttributes(looseObjectPath, FileAttributes.Normal);
        File.Delete(looseObjectPath);

        var exception = await AssertThrowsAsync<SourceContextException>(
            async () => await reader.ReadBlobAsync(context, entry, 1024));
        Assert.AreEqual(SourceContextFailureCode.ObjectMissing, exception.Code);
    }

    [TestMethod]
    public async Task Git_child_process_receives_local_only_replacement_and_lock_controls()
    {
        var prefix = StubPrefix();
        var runner = new GitProcessRunner(TimeSpan.FromSeconds(10), DotnetHost(), prefix, addGitGlobalOptions: false);
        var result = await runner.RunAsync(_workingRepository, ["environment"], 1024);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("1|1|0", result.StandardOutputText().Trim());

        var startInfo = GitProcessRunner.CreateStartInfo(_workingRepository, ["rev-parse", "HEAD"]);
        Assert.AreEqual("1", startInfo.Environment["GIT_NO_LAZY_FETCH"]);
        Assert.AreEqual("1", startInfo.Environment["GIT_NO_REPLACE_OBJECTS"]);
        Assert.AreEqual("0", startInfo.Environment["GIT_OPTIONAL_LOCKS"]);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.ArgumentList.Contains("--no-pager"));
    }

    [TestMethod]
    public async Task Caller_cancellation_kills_the_git_child_process_tree()
    {
        var pidFile = Path.Combine(_testRoot, "child-process-id.txt");
        var runner = new GitProcessRunner(TimeSpan.FromSeconds(20), DotnetHost(), StubPrefix(), addGitGlobalOptions: false);
        using var cancellation = new CancellationTokenSource();
        var operation = runner.RunAsync(_workingRepository, ["hold", pidFile], 1024, cancellation.Token);

        await WaitForFileAsync(pidFile, TimeSpan.FromSeconds(10));
        var processId = int.Parse(await File.ReadAllTextAsync(pidFile), CultureInfo.InvariantCulture);
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(async () => await operation);

        Assert.IsFalse(IsProcessRunning(processId));
    }

    [TestMethod]
    public async Task Internal_timeout_kills_the_git_child_process_tree_and_maps_distinctly()
    {
        var pidFile = Path.Combine(_testRoot, "timeout-process-id.txt");
        var runner = new GitProcessRunner(TimeSpan.FromSeconds(3), DotnetHost(), StubPrefix(), addGitGlobalOptions: false);
        var operation = runner.RunAsync(_workingRepository, ["hold", pidFile], 1024);

        await WaitForFileAsync(pidFile, TimeSpan.FromSeconds(10));
        var processId = int.Parse(await File.ReadAllTextAsync(pidFile), CultureInfo.InvariantCulture);
        var exception = await AssertThrowsAsync<SourceContextException>(async () => await operation);
        Assert.AreEqual(SourceContextFailureCode.GitProcessTimedOut, exception.Code);
        Assert.IsFalse(IsProcessRunning(processId));
    }

    [TestMethod]
    public async Task Bounded_process_output_kills_a_noisy_child()
    {
        var pidFile = Path.Combine(_testRoot, "overflow-process-id.txt");
        var runner = new GitProcessRunner(TimeSpan.FromSeconds(10), DotnetHost(), StubPrefix(), addGitGlobalOptions: false);
        var operation = runner.RunAsync(_workingRepository, ["overflow", pidFile], 64);

        await WaitForFileAsync(pidFile, TimeSpan.FromSeconds(10));
        var processId = int.Parse(await File.ReadAllTextAsync(pidFile), CultureInfo.InvariantCulture);
        var exception = await AssertThrowsAsync<SourceContextException>(async () => await operation);

        Assert.AreEqual(SourceContextFailureCode.InvalidGitOutput, exception.Code);
        Assert.IsFalse(IsProcessRunning(processId));
    }

    [TestMethod]
    public async Task Search_pattern_with_argument_separator_content_is_still_a_literal_pattern()
    {
        await File.WriteAllTextAsync(Path.Combine(_workingRepository, "dash.txt"), "--help marker\n", Utf8WithoutBom);
        await RunGitAsync(_workingRepository, "add", "dash.txt");
        await RunGitAsync(_workingRepository, "commit", "-m", "synthetic literal pattern");
        var reader = CreateReader(_workingRepository);
        var context = (await reader.ResolveForProjectAsync(42))!;

        var result = await reader.SearchAsync(context, "--help", ["dash.txt"], 10);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("--help marker", result[0].MatchedText);
    }

    private GitRepositoryReader CreateReader(string repositoryPath, string revision = "HEAD") =>
        new(new GitRepositoryCatalog([Mapping("synthetic", repositoryPath, [42], revision)]));

    private static GitRepositoryMappingOptions Mapping(string key, string path, long[] projectIds, string? revision = "HEAD") => new()
    {
        Key = key,
        Path = path,
        RedmineProjectIds = projectIds,
        Revision = revision
    };

    private static TException AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        Assert.Fail($"Expected {typeof(TException).Name}.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        Assert.Fail($"Expected {typeof(TException).Name}.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }
    private static string DotnetHost() => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IssueHarbor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The IssueHarbor test repository root could not be located.");
    }

    private static IReadOnlyList<string> StubPrefix()
    {
        var runtimeConfig = Path.Combine(AppContext.BaseDirectory, "IssueHarbor.Analyzer.Tests.runtimeconfig.json");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var stubAssembly = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "Analyzer.Tests",
            "SourceContext",
            "GitProcessStub",
            "bin",
            configuration,
            "net10.0",
            "GitProcessStub.dll");
        if (!File.Exists(stubAssembly))
        {
            throw new FileNotFoundException("The synthetic process stub was not built.");
        }

        return ["exec", "--runtimeconfig", runtimeConfig, stubAssembly];
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!File.Exists(path) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(File.Exists(path), "The synthetic child process did not start in time.");
    }

    private static async Task<GitFixtureResult> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start synthetic Git fixture process.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Synthetic Git fixture command failed ({process.ExitCode}): {stderrTask.Result}");
        }

        return new GitFixtureResult(stdoutTask.Result, stderrTask.Result);
    }

    private sealed record GitFixtureResult(string StandardOutput, string StandardError);
}

internal static class GitProcessResultTestExtensions
{
    public static string StandardOutputText(this GitProcessResult result) => Encoding.UTF8.GetString(result.StandardOutput);
}
