using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IssueHarbor.RedmineMcp;

internal sealed class RedmineMcpReadPath(
    RedmineMcpOptions options,
    HttpClient httpClient,
    IRedmineMcpDiagnostics diagnostics)
{
    internal async Task<IssueListResult> ListIssuesAsync(
        long? query_id,
        long[]? issue_ids,
        string? status,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        return await RedmineMcpOperation.RunAsync("issues_list", cancellationToken, diagnostics, async () =>
        {
            RedmineMcpManifest.ValidateIssueListInputs(query_id, issue_ids, status, offset, limit);
            var query = new List<KeyValuePair<string, string>>();
            if (query_id.HasValue)
            {
                query.Add(new("query_id", query_id.Value.ToString(CultureInfo.InvariantCulture)));
            }
            else if (issue_ids is not null)
            {
                query.Add(new("issue_id", string.Join(',', issue_ids.Select(id => id.ToString(CultureInfo.InvariantCulture)))));
                query.Add(new("status_id", "*"));
            }
            else
            {
                query.Add(new("status_id", status switch
                {
                    "open" => "open",
                    "closed" => "closed",
                    "all" => "*",
                    _ => throw RedmineMcpFailure.InvalidArgument(),
                }));
            }

            query.Add(new("offset", offset.ToString(CultureInfo.InvariantCulture)));
            query.Add(new("limit", limit.ToString(CultureInfo.InvariantCulture)));

            using var response = await SendGetAsync(BuildUri("issues.json", query), cancellationToken).ConfigureAwait(false);
            using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            return RedmineMcpNormalizer.NormalizeIssueList(document.RootElement, offset, limit);
        }).ConfigureAwait(false);
    }

    internal async Task<IssueDetail> GetIssueAsync(long issueId, CancellationToken cancellationToken)
    {
        return await RedmineMcpOperation.RunAsync("issue_get", cancellationToken, diagnostics, async () =>
        {
            RedmineMcpManifest.ValidatePositiveId(issueId, "issue_id");
            var query = BuildQuery([new("include", "children,attachments,relations,journals")]);
            var uri = new Uri(options.BaseUri, $"issues/{issueId.ToString(CultureInfo.InvariantCulture)}.json?{query}");
            using var response = await SendGetAsync(uri, cancellationToken).ConfigureAwait(false);
            using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            return RedmineMcpNormalizer.NormalizeIssueDetail(document.RootElement, issueId);
        }).ConfigureAwait(false);
    }

    internal async Task<QueryListResult> ListQueriesAsync(CancellationToken cancellationToken)
    {
        return await RedmineMcpOperation.RunAsync("queries_list", cancellationToken, diagnostics, async () =>
        {
            const int pageSize = 100;
            var allQueries = new List<SavedQuery>();
            int? expectedTotal = null;
            var offset = 0;

            while (true)
            {
                var query = new List<KeyValuePair<string, string>>
                {
                    new("offset", offset.ToString(CultureInfo.InvariantCulture)),
                    new("limit", pageSize.ToString(CultureInfo.InvariantCulture)),
                };
                using var response = await SendGetAsync(BuildUri("queries.json", query), cancellationToken).ConfigureAwait(false);
                using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
                var page = RedmineMcpNormalizer.NormalizeQueryPage(document.RootElement, offset, pageSize);

                if (expectedTotal.HasValue && page.TotalCount != expectedTotal.Value)
                {
                    throw RedmineMcpFailure.InvalidUpstream();
                }

                expectedTotal ??= page.TotalCount;
                allQueries.AddRange(page.Queries);

                if (allQueries.Count >= expectedTotal.Value)
                {
                    if (allQueries.Count != expectedTotal.Value)
                    {
                        throw RedmineMcpFailure.InvalidUpstream();
                    }

                    return new QueryListResult(allQueries);
                }

                if (page.Queries.Count == 0 || page.Offset != offset || page.Limit <= 0 || page.Limit > pageSize)
                {
                    throw RedmineMcpFailure.InvalidUpstream();
                }

                var nextOffset = checked(page.Offset + page.Queries.Count);
                if (nextOffset <= offset || nextOffset > page.TotalCount)
                {
                    throw RedmineMcpFailure.InvalidUpstream();
                }

                offset = nextOffset;
            }
        }).ConfigureAwait(false);
    }

    internal async Task<AttachmentResult> GetAttachmentAsync(long attachmentId, CancellationToken cancellationToken)
    {
        return await RedmineMcpOperation.RunAsync("attachment_get", cancellationToken, diagnostics, async () =>
        {
            RedmineMcpManifest.ValidatePositiveId(attachmentId, "attachment_id");
            var metadataUri = new Uri(options.BaseUri, $"attachments/{attachmentId.ToString(CultureInfo.InvariantCulture)}.json");
            using var metadataResponse = await SendGetAsync(metadataUri, cancellationToken).ConfigureAwait(false);
            using var metadataDocument = await ReadJsonAsync(metadataResponse, cancellationToken).ConfigureAwait(false);
            var metadata = RedmineMcpNormalizer.NormalizeAttachmentEnvelope(metadataDocument.RootElement, attachmentId, out var contentUrl);

            if (metadata.FileSize > options.MaxAttachmentBytes || metadata.FileSize > Array.MaxLength)
            {
                throw RedmineMcpFailure.InvalidUpstream();
            }

            var contentUri = ValidateAttachmentOrigin(contentUrl);
            var content = await GetAttachmentContentAsync(contentUri, metadata.FileSize, cancellationToken).ConfigureAwait(false);
            var mimeType = string.IsNullOrWhiteSpace(metadata.ContentType) ? "application/octet-stream" : metadata.ContentType;
            return new AttachmentResult(metadata, content, mimeType);
        }).ConfigureAwait(false);
    }

    private async Task<byte[]> GetAttachmentContentAsync(Uri contentUri, long expectedSize, CancellationToken cancellationToken)
    {
        using var response = await SendGetAsync(contentUri, cancellationToken).ConfigureAwait(false);
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is < 0 || contentLength > options.MaxAttachmentBytes || (contentLength.HasValue && contentLength.Value != expectedSize))
        {
            throw RedmineMcpFailure.InvalidUpstream(response.StatusCode);
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var remainingCeiling = Math.Min(options.MaxAttachmentBytes - total, expectedSize - total);
            var readCapacity = remainingCeiling >= buffer.Length
                ? buffer.Length
                : checked((int)remainingCeiling + 1);
            var read = await input.ReadAsync(buffer.AsMemory(0, readCapacity), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > options.MaxAttachmentBytes || total > expectedSize)
            {
                throw RedmineMcpFailure.InvalidUpstream(response.StatusCode);
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (total != expectedSize || (contentLength.HasValue && total != contentLength.Value))
        {
            throw RedmineMcpFailure.InvalidUpstream(response.StatusCode);
        }

        return output.ToArray();
    }

    private async Task<HttpResponseMessage> SendGetAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Redmine-API-Key", options.ApiKey);

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var failure = RedmineMcpFailure.FromStatus(response.StatusCode);
            response.Dispose();
            throw failure;
        }

        return response;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Uri ValidateAttachmentOrigin(string contentUrl)
    {
        if (!Uri.TryCreate(contentUrl, UriKind.Absolute, out var contentUri) ||
            (contentUri.Scheme != Uri.UriSchemeHttp && contentUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(contentUri.UserInfo) ||
            RedmineMcpOptions.HasUserInfoSyntax(contentUrl) ||
            !SameOrigin(options.BaseUri, contentUri))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return contentUri;
    }

    private static bool SameOrigin(Uri expected, Uri actual) =>
        string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.IdnHost, actual.IdnHost, StringComparison.OrdinalIgnoreCase) &&
        expected.Port == actual.Port;

    private Uri BuildUri(string relativePath, IReadOnlyList<KeyValuePair<string, string>> query)
    {
        var queryString = BuildQuery(query);
        return new Uri(options.BaseUri, relativePath + "?" + queryString);
    }

    private static string BuildQuery(IReadOnlyList<KeyValuePair<string, string>> query) =>
        string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
}
