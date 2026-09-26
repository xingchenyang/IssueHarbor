using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssueHarbor.RedmineMcp;

internal sealed record NamedReference(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name);

internal sealed record IssueSummary(
    [property: JsonPropertyName("issue_id")] long IssueId,
    [property: JsonPropertyName("project")] NamedReference Project,
    [property: JsonPropertyName("tracker")] NamedReference Tracker,
    [property: JsonPropertyName("status")] NamedReference Status,
    [property: JsonPropertyName("priority")] NamedReference Priority,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("author")] NamedReference Author,
    [property: JsonPropertyName("assigned_to")] NamedReference? AssignedTo,
    [property: JsonPropertyName("created_on")] string CreatedOn,
    [property: JsonPropertyName("updated_on")] string UpdatedOn);

internal sealed record IssueListResult(
    [property: JsonPropertyName("total_count")] int TotalCount,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("issues")] IReadOnlyList<IssueSummary> Issues);

internal sealed record CustomFieldValue(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("multiple")] bool Multiple,
    [property: JsonPropertyName("values")] IReadOnlyList<JsonElement> Values);

internal sealed record JournalDetail(
    [property: JsonPropertyName("property")] string Property,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("old_value")] JsonElement? OldValue,
    [property: JsonPropertyName("new_value")] JsonElement? NewValue);

internal sealed record IssueJournal(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("user")] NamedReference? User,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("created_on")] string CreatedOn,
    [property: JsonPropertyName("updated_on")] string? UpdatedOn,
    [property: JsonPropertyName("updated_by")] NamedReference? UpdatedBy,
    [property: JsonPropertyName("private_notes")] bool? PrivateNotes,
    [property: JsonPropertyName("details")] IReadOnlyList<JournalDetail> Details);

internal sealed record AttachmentMetadata(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("filesize")] long FileSize,
    [property: JsonPropertyName("content_type")] string? ContentType,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("author")] NamedReference? Author,
    [property: JsonPropertyName("created_on")] string? CreatedOn);

internal sealed record IssueRelation(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("relation_type")] string RelationType,
    [property: JsonPropertyName("issue_id")] long IssueId,
    [property: JsonPropertyName("issue_to_id")] long IssueToId);

internal sealed record IssueChild(
    [property: JsonPropertyName("issue_id")] long IssueId,
    [property: JsonPropertyName("tracker")] NamedReference Tracker,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("children")] IReadOnlyList<IssueChild> Children);

internal sealed record IssueDetail(
    [property: JsonPropertyName("issue_id")] long IssueId,
    [property: JsonPropertyName("project")] NamedReference Project,
    [property: JsonPropertyName("tracker")] NamedReference Tracker,
    [property: JsonPropertyName("status")] NamedReference Status,
    [property: JsonPropertyName("priority")] NamedReference Priority,
    [property: JsonPropertyName("author")] NamedReference Author,
    [property: JsonPropertyName("assigned_to")] NamedReference? AssignedTo,
    [property: JsonPropertyName("category")] NamedReference? Category,
    [property: JsonPropertyName("fixed_version")] NamedReference? FixedVersion,
    [property: JsonPropertyName("parent_issue_id")] long? ParentIssueId,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("start_date")] string? StartDate,
    [property: JsonPropertyName("due_date")] string? DueDate,
    [property: JsonPropertyName("done_ratio")] int DoneRatio,
    [property: JsonPropertyName("is_private")] bool IsPrivate,
    [property: JsonPropertyName("estimated_hours")] decimal? EstimatedHours,
    [property: JsonPropertyName("created_on")] string CreatedOn,
    [property: JsonPropertyName("updated_on")] string UpdatedOn,
    [property: JsonPropertyName("closed_on")] string? ClosedOn,
    [property: JsonPropertyName("custom_fields")] IReadOnlyList<CustomFieldValue> CustomFields,
    [property: JsonPropertyName("journals")] IReadOnlyList<IssueJournal> Journals,
    [property: JsonPropertyName("attachments")] IReadOnlyList<AttachmentMetadata> Attachments,
    [property: JsonPropertyName("relations")] IReadOnlyList<IssueRelation> Relations,
    [property: JsonPropertyName("children")] IReadOnlyList<IssueChild> Children);

internal sealed record SavedQuery(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("is_public")] bool? IsPublic,
    [property: JsonPropertyName("project_id")] long? ProjectId);

internal sealed record QueryListResult([property: JsonPropertyName("queries")] IReadOnlyList<SavedQuery> Queries);

internal sealed record AttachmentResult(
    AttachmentMetadata Metadata,
    byte[] Content,
    string MimeType);

internal sealed record AttachmentOutput([property: JsonPropertyName("attachment")] AttachmentMetadata Attachment);
