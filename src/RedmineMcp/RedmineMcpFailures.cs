using System.Diagnostics;
using System.Net;

namespace IssueHarbor.RedmineMcp;

internal static class RedmineMcpErrorCategories
{
    internal const string NotFound = "not_found";
    internal const string AuthenticationRequired = "authentication_required";
    internal const string Forbidden = "forbidden";
    internal const string UpstreamUnavailable = "upstream_unavailable";
    internal const string InvalidUpstreamResponse = "invalid_upstream_response";
    internal const string InvalidArgument = "invalid_argument";
}

internal sealed class RedmineMcpFailure(string category, HttpStatusCode? statusCode = null) : Exception(category)
{
    internal string Category { get; } = category;
    internal HttpStatusCode? StatusCode { get; } = statusCode;

    internal static RedmineMcpFailure InvalidArgument() => new(RedmineMcpErrorCategories.InvalidArgument);

    internal static RedmineMcpFailure InvalidUpstream(HttpStatusCode? statusCode = null) =>
        new(RedmineMcpErrorCategories.InvalidUpstreamResponse, statusCode);

    internal static RedmineMcpFailure FromStatus(HttpStatusCode statusCode)
    {
        var numericStatus = (int)statusCode;
        var category = numericStatus switch
        {
            401 => RedmineMcpErrorCategories.AuthenticationRequired,
            403 => RedmineMcpErrorCategories.Forbidden,
            404 => RedmineMcpErrorCategories.NotFound,
            408 or 429 => RedmineMcpErrorCategories.UpstreamUnavailable,
            >= 500 => RedmineMcpErrorCategories.UpstreamUnavailable,
            >= 400 => RedmineMcpErrorCategories.InvalidUpstreamResponse,
            >= 300 => RedmineMcpErrorCategories.InvalidUpstreamResponse,
            _ => RedmineMcpErrorCategories.InvalidUpstreamResponse,
        };

        return new RedmineMcpFailure(category, statusCode);
    }
}

internal interface IRedmineMcpDiagnostics
{
    void RecordFailure(string operation, HttpStatusCode? statusCode, string category, long elapsedMilliseconds);
}

internal sealed class StderrRedmineMcpDiagnostics(TextWriter writer) : IRedmineMcpDiagnostics
{
    private readonly object _gate = new();

    internal static StderrRedmineMcpDiagnostics Instance { get; } = new(Console.Error);

    public void RecordFailure(string operation, HttpStatusCode? statusCode, string category, long elapsedMilliseconds)
    {
        lock (_gate)
        {
            writer.WriteLine("RedmineMcp operation={0} status={1} error={2} duration_ms={3}",
                operation,
                statusCode?.ToString() ?? "none",
                category,
                elapsedMilliseconds);
        }
    }
}

internal static class RedmineMcpOperation
{
    internal static async Task<T> RunAsync<T>(
        string operation,
        CancellationToken cancellationToken,
        IRedmineMcpDiagnostics diagnostics,
        Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedmineMcpFailure failure)
        {
            diagnostics.RecordFailure(operation, failure.StatusCode, failure.Category, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException)
        {
            const string category = RedmineMcpErrorCategories.UpstreamUnavailable;
            diagnostics.RecordFailure(operation, null, category, stopwatch.ElapsedMilliseconds);
            throw new RedmineMcpFailure(category);
        }
        catch (HttpRequestException)
        {
            const string category = RedmineMcpErrorCategories.UpstreamUnavailable;
            diagnostics.RecordFailure(operation, null, category, stopwatch.ElapsedMilliseconds);
            throw new RedmineMcpFailure(category);
        }
        catch (IOException)
        {
            const string category = RedmineMcpErrorCategories.UpstreamUnavailable;
            diagnostics.RecordFailure(operation, null, category, stopwatch.ElapsedMilliseconds);
            throw new RedmineMcpFailure(category);
        }
        catch (System.Text.Json.JsonException)
        {
            const string category = RedmineMcpErrorCategories.InvalidUpstreamResponse;
            diagnostics.RecordFailure(operation, null, category, stopwatch.ElapsedMilliseconds);
            throw RedmineMcpFailure.InvalidUpstream();
        }
    }
}
