using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace IssueHarbor.RedmineMcp;

internal sealed class RedmineMcpToolHandlers(RedmineMcpReadPath readPath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    internal Task<CallToolResult> IssuesList(
        [Description("Saved Redmine query identifier. Select exactly one selector.")]
        [Range(1, long.MaxValue)]
        long? query_id = null,
        [Description("One to 100 positive Redmine Issue identifiers. Select exactly one selector.")]
        [MinLength(1)]
        [MaxLength(RedmineMcpManifest.RedmineSinglePageMaximum)]
        long[]? issue_ids = null,
        [Description("Issue status selector: open, closed, or all. Select exactly one selector.")]
        [AllowedValues("open", "closed", "all")]
        string? status = null,
        [Description("Zero-based result offset.")]
        [Range(0, int.MaxValue)]
        int offset = 0,
        [Description("Page size from 1 through 100.")]
        [Range(1, RedmineMcpManifest.RedmineSinglePageMaximum)]
        int limit = RedmineMcpManifest.RedmineSinglePageMaximum,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => await readPath.ListIssuesAsync(query_id, issue_ids, status, offset, limit, cancellationToken).ConfigureAwait(false), cancellationToken);

    internal Task<CallToolResult> IssueGet(
        [Description("Positive integer Redmine Issue identifier.")]
        [Range(1, long.MaxValue)]
        long issue_id,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => await readPath.GetIssueAsync(issue_id, cancellationToken).ConfigureAwait(false), cancellationToken);

    internal Task<CallToolResult> QueriesList(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => await readPath.ListQueriesAsync(cancellationToken).ConfigureAwait(false), cancellationToken);

    internal Task<CallToolResult> AttachmentGet(
        [Description("Positive integer Redmine attachment identifier.")]
        [Range(1, long.MaxValue)]
        long attachment_id,
        CancellationToken cancellationToken = default) =>
        ExecuteAttachmentAsync(attachment_id, cancellationToken);

    private async Task<CallToolResult> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            var output = await action().ConfigureAwait(false);
            var structured = JsonSerializer.SerializeToElement(output, SerializerOptions);
            return new CallToolResult
            {
                IsError = false,
                Content = [new TextContentBlock { Text = structured.GetRawText() }],
                StructuredContent = structured,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedmineMcpFailure failure)
        {
            return ErrorResult(failure.Category);
        }
        catch (Exception)
        {
            return ErrorResult(RedmineMcpErrorCategories.InvalidUpstreamResponse);
        }
    }

    private async Task<CallToolResult> ExecuteAttachmentAsync(long attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            var attachment = await readPath.GetAttachmentAsync(attachmentId, cancellationToken).ConfigureAwait(false);
            var metadata = new AttachmentOutput(attachment.Metadata);
            var structured = JsonSerializer.SerializeToElement(metadata, SerializerOptions);
            var blob = BlobResourceContents.FromBytes(
                attachment.Content,
                $"issueharbor://attachments/{attachment.Metadata.Id}",
                attachment.MimeType);

            return new CallToolResult
            {
                IsError = false,
                Content =
                [
                    new TextContentBlock { Text = structured.GetRawText() },
                    new EmbeddedResourceBlock { Resource = blob },
                ],
                StructuredContent = structured,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedmineMcpFailure failure)
        {
            return ErrorResult(failure.Category);
        }
        catch (Exception)
        {
            return ErrorResult(RedmineMcpErrorCategories.InvalidUpstreamResponse);
        }
    }

    private static CallToolResult ErrorResult(string category) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = category }],
    };
}
