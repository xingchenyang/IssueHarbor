using System.Diagnostics;
using System.Text;

namespace IssueHarbor.Analyzer.SourceContext;

internal sealed record GitProcessResult(byte[] StandardOutput, string StandardError, int ExitCode);

internal sealed class GitProcessRunner
{
    private const int MaximumStandardErrorBytes = 64 * 1024;

    private static readonly string[] RemovedEnvironmentNames =
    [
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_COMMON_DIR",
        "GIT_INDEX_FILE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_CEILING_DIRECTORIES",
        "GIT_CONFIG_PARAMETERS",
        "GIT_CONFIG_COUNT",
        "GIT_TRACE",
        "GIT_TRACE_SETUP",
        "GIT_TRACE_PACKET",
        "GIT_TRACE_PERFORMANCE",
        "GIT_TRACE_CURL",
        "GIT_TRACE_CURL_NO_DATA",
        "GIT_TRACE2",
        "GIT_TRACE2_EVENT",
        "GIT_TRACE2_PERF",
        "GIT_SSH_COMMAND"
    ];

    private readonly TimeSpan _timeout;
    private readonly string _executable;
    private readonly IReadOnlyList<string> _prefixArguments;
    private readonly bool _addGitGlobalOptions;

    public GitProcessRunner(TimeSpan? timeout = null)
        : this(timeout, "git", [], addGitGlobalOptions: true)
    {
    }

    internal GitProcessRunner(
        TimeSpan? timeout,
        string executable,
        IReadOnlyList<string> prefixArguments,
        bool addGitGlobalOptions)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(prefixArguments);
        _executable = executable;
        _prefixArguments = prefixArguments.ToArray();
        _addGitGlobalOptions = addGitGlobalOptions;
    }

    public async Task<GitProcessResult> RunAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        long maximumOutputBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(arguments);
        if (maximumOutputBytes < 0 || maximumOutputBytes > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = CreateStartInfo(repositoryPath, arguments, _executable, _prefixArguments, _addGitGlobalOptions)
        };
        try
        {
            if (!process.Start())
            {
                throw GitStartFailure();
            }
        }
        catch (SourceContextException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            throw GitStartFailure();
        }

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        var stdoutTask = ReadBoundedStreamAsync(process.StandardOutput.BaseStream, process, maximumOutputBytes, linkedSource.Token);
        var stderrTask = ReadBoundedStreamAsync(process.StandardError.BaseStream, process, MaximumStandardErrorBytes, linkedSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await StopProcessTreeAsync(process).ConfigureAwait(false);
            await DrainAfterStopAsync(stdoutTask, stderrTask).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            throw new SourceContextException(SourceContextFailureCode.GitProcessTimedOut, "Git operation timed out.");
        }
        catch (GitOutputLimitException)
        {
            await StopProcessTreeAsync(process).ConfigureAwait(false);
            await DrainAfterStopAsync(stdoutTask, stderrTask).ConfigureAwait(false);
            throw new SourceContextException(SourceContextFailureCode.InvalidGitOutput, "Git output exceeded the configured read bound.");
        }

        return new GitProcessResult(
            await stdoutTask.ConfigureAwait(false),
            Encoding.UTF8.GetString(await stderrTask.ConfigureAwait(false)),
            process.ExitCode);
    }

    internal static ProcessStartInfo CreateStartInfo(string repositoryPath, IReadOnlyList<string> arguments)
        => CreateStartInfo(repositoryPath, arguments, "git", [], addGitGlobalOptions: true);

    private static ProcessStartInfo CreateStartInfo(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        string executable,
        IReadOnlyList<string> prefixArguments,
        bool addGitGlobalOptions)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = repositoryPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var prefixArgument in prefixArguments)
        {
            if (prefixArgument.IndexOf('\0') >= 0)
            {
                throw new ArgumentException("Git arguments cannot contain null values.", nameof(prefixArguments));
            }

            startInfo.ArgumentList.Add(prefixArgument);
        }

        if (addGitGlobalOptions)
        {
            startInfo.ArgumentList.Add("--no-pager");
        }

        foreach (var argument in arguments)
        {
            if (argument is null || argument.IndexOf('\0') >= 0)
            {
                throw new ArgumentException("Git arguments cannot contain null values.", nameof(arguments));
            }

            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environmentName in RemovedEnvironmentNames)
        {
            startInfo.Environment.Remove(environmentName);
        }

        foreach (var environmentName in startInfo.Environment.Keys
                     .Where(static name => name.StartsWith("GIT_CONFIG_KEY_", StringComparison.OrdinalIgnoreCase)
                         || name.StartsWith("GIT_CONFIG_VALUE_", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(environmentName);
        }

        startInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        startInfo.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return startInfo;
    }

    private static async Task<byte[]> ReadBoundedStreamAsync(
        Stream stream,
        Process process,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[32 * 1024];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return output.ToArray();
            }

            if (output.Length > maximumBytes - bytesRead)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }

                throw new GitOutputLimitException();
            }

            output.Write(buffer, 0, bytesRead);
        }
    }

    private static async Task StopProcessTreeAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task DrainAfterStopAsync(params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (GitOutputLimitException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static SourceContextException GitStartFailure() =>
        new(SourceContextFailureCode.GitCommandFailed, "The Git executable could not be started.");

    private sealed class GitOutputLimitException : Exception
    {
    }
}
