using System.Globalization;
using System.Text.Json;

namespace IssueHarbor.RedmineMcp;

internal sealed record SavedQueryPage(int TotalCount, int Offset, int Limit, IReadOnlyList<SavedQuery> Queries);

internal static class RedmineMcpNormalizer
{
    internal static IssueListResult NormalizeIssueList(JsonElement root, int requestedOffset, int requestedLimit)
    {
        RequireObject(root);
        var totalCount = RequiredNonNegativeInt(root, "total_count");
        var offset = RequiredNonNegativeInt(root, "offset");
        var limit = RequiredNonNegativeInt(root, "limit");
        if (offset != requestedOffset || limit != requestedLimit || limit is < 1 or > 100)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        var issueElements = RequiredArray(root, "issues");
        if (issueElements.GetArrayLength() > requestedLimit || issueElements.GetArrayLength() > totalCount)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        var issues = issueElements.EnumerateArray().Select(NormalizeIssueSummary).ToArray();
        return new IssueListResult(totalCount, offset, limit, issues);
    }

    internal static IssueDetail NormalizeIssueDetail(JsonElement root, long requestedIssueId)
    {
        RequireObject(root);
        var issue = RequiredObject(root, "issue");
        var normalized = NormalizeIssue(issue);
        if (normalized.IssueId != requestedIssueId)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return normalized;
    }

    internal static SavedQueryPage NormalizeQueryPage(JsonElement root, int requestedOffset, int requestedLimit)
    {
        RequireObject(root);
        var totalCount = RequiredNonNegativeInt(root, "total_count");
        var offset = RequiredNonNegativeInt(root, "offset");
        var limit = RequiredNonNegativeInt(root, "limit");
        if (offset != requestedOffset || limit is < 1 or > 100 || limit > requestedLimit)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        var elements = RequiredArray(root, "queries");
        if (elements.GetArrayLength() > limit || (elements.GetArrayLength() == 0 && offset < totalCount))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        var queries = elements.EnumerateArray().Select(NormalizeSavedQuery).ToArray();
        return new SavedQueryPage(totalCount, offset, limit, queries);
    }

    internal static AttachmentMetadata NormalizeAttachmentEnvelope(
        JsonElement root,
        long requestedAttachmentId,
        out string contentUrl)
    {
        RequireObject(root);
        var attachment = RequiredObject(root, "attachment");
        contentUrl = RequiredString(attachment, "content_url");
        var metadata = NormalizeAttachment(attachment);
        if (metadata.Id != requestedAttachmentId)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return metadata;
    }

    private static IssueSummary NormalizeIssueSummary(JsonElement issue)
    {
        RequireObject(issue);
        return new IssueSummary(
            RequiredPositiveId(issue, "id"),
            RequiredReference(issue, "project"),
            RequiredReference(issue, "tracker"),
            RequiredReference(issue, "status"),
            RequiredReference(issue, "priority"),
            RequiredString(issue, "subject"),
            RequiredReference(issue, "author"),
            OptionalReference(issue, "assigned_to"),
            RequiredTimestamp(issue, "created_on"),
            RequiredTimestamp(issue, "updated_on"));
    }

    private static IssueDetail NormalizeIssue(JsonElement issue)
    {
        var customFields = OptionalArray(issue, "custom_fields").EnumerateArray()
            .Select(NormalizeCustomField).ToArray();
        var journals = OptionalArray(issue, "journals").EnumerateArray()
            .Select(NormalizeJournal).ToArray();
        var attachments = OptionalArray(issue, "attachments").EnumerateArray()
            .Select(NormalizeAttachment).ToArray();
        var relations = OptionalArray(issue, "relations").EnumerateArray()
            .Select(NormalizeRelation).ToArray();
        var children = OptionalArray(issue, "children").EnumerateArray()
            .Select(NormalizeChild).ToArray();

        return new IssueDetail(
            RequiredPositiveId(issue, "id"),
            RequiredReference(issue, "project"),
            RequiredReference(issue, "tracker"),
            RequiredReference(issue, "status"),
            RequiredReference(issue, "priority"),
            RequiredReference(issue, "author"),
            OptionalReference(issue, "assigned_to"),
            OptionalReference(issue, "category"),
            OptionalReference(issue, "fixed_version"),
            OptionalParentIssueId(issue),
            RequiredString(issue, "subject"),
            OptionalString(issue, "description"),
            OptionalDateOnly(issue, "start_date"),
            OptionalDateOnly(issue, "due_date"),
            RequiredInt(issue, "done_ratio"),
            RequiredBoolean(issue, "is_private"),
            OptionalDecimal(issue, "estimated_hours"),
            RequiredTimestamp(issue, "created_on"),
            RequiredTimestamp(issue, "updated_on"),
            OptionalTimestamp(issue, "closed_on"),
            customFields,
            journals,
            attachments,
            relations,
            children);
    }

    private static CustomFieldValue NormalizeCustomField(JsonElement element)
    {
        RequireObject(element);
        var valuePresent = element.TryGetProperty("value", out var rawValue);
        var values = new List<JsonElement>();
        var isMultipleValueShape = false;

        if (valuePresent && rawValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (rawValue.ValueKind == JsonValueKind.Array)
            {
                isMultipleValueShape = true;
                foreach (var item in rawValue.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Undefined)
                    {
                        throw RedmineMcpFailure.InvalidUpstream();
                    }

                    if (item.ValueKind != JsonValueKind.Null)
                    {
                        values.Add(item.Clone());
                    }
                }
            }
            else if (rawValue.ValueKind is JsonValueKind.Object or JsonValueKind.Undefined)
            {
                throw RedmineMcpFailure.InvalidUpstream();
            }
            else
            {
                values.Add(rawValue.Clone());
            }
        }

        return new CustomFieldValue(
            RequiredPositiveId(element, "id"),
            RequiredString(element, "name"),
            isMultipleValueShape,
            values);
    }

    private static IssueJournal NormalizeJournal(JsonElement journal)
    {
        RequireObject(journal);
        var details = OptionalArray(journal, "details").EnumerateArray()
            .Select(NormalizeJournalDetail).ToArray();

        return new IssueJournal(
            RequiredPositiveId(journal, "id"),
            OptionalReference(journal, "user"),
            OptionalString(journal, "notes"),
            RequiredTimestamp(journal, "created_on"),
            OptionalTimestamp(journal, "updated_on"),
            OptionalReference(journal, "updated_by"),
            OptionalBoolean(journal, "private_notes"),
            details);
    }

    private static JournalDetail NormalizeJournalDetail(JsonElement detail)
    {
        RequireObject(detail);
        return new JournalDetail(
            RequiredString(detail, "property"),
            RequiredString(detail, "name"),
            OptionalJournalValue(detail, "old_value"),
            OptionalJournalValue(detail, "new_value"));
    }

    private static JsonElement? OptionalJournalValue(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.Clone();
        }

        if (value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String))
        {
            return value.Clone();
        }

        throw RedmineMcpFailure.InvalidUpstream();
    }

    private static AttachmentMetadata NormalizeAttachment(JsonElement attachment)
    {
        RequireObject(attachment);
        return new AttachmentMetadata(
            RequiredPositiveId(attachment, "id"),
            RequiredString(attachment, "filename"),
            RequiredNonNegativeLong(attachment, "filesize"),
            OptionalString(attachment, "content_type"),
            OptionalString(attachment, "description"),
            OptionalReference(attachment, "author"),
            OptionalTimestamp(attachment, "created_on"));
    }

    private static IssueRelation NormalizeRelation(JsonElement relation)
    {
        RequireObject(relation);
        return new IssueRelation(
            RequiredPositiveId(relation, "id"),
            RequiredString(relation, "relation_type"),
            RequiredPositiveId(relation, "issue_id"),
            RequiredPositiveId(relation, "issue_to_id"));
    }

    private static IssueChild NormalizeChild(JsonElement child)
    {
        RequireObject(child);
        return new IssueChild(
            RequiredPositiveId(child, "id"),
            RequiredReference(child, "tracker"),
            RequiredString(child, "subject"),
            OptionalArray(child, "children").EnumerateArray().Select(NormalizeChild).ToArray());
    }

    private static SavedQuery NormalizeSavedQuery(JsonElement query)
    {
        RequireObject(query);
        return new SavedQuery(
            RequiredPositiveId(query, "id"),
            RequiredString(query, "name"),
            OptionalBoolean(query, "is_public"),
            OptionalPositiveId(query, "project_id"));
    }

    private static NamedReference RequiredReference(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var reference) || reference.ValueKind != JsonValueKind.Object)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return new NamedReference(RequiredPositiveId(reference, "id"), RequiredString(reference, "name"));
    }

    private static NamedReference? OptionalReference(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var reference) || reference.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (reference.ValueKind != JsonValueKind.Object)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return new NamedReference(RequiredPositiveId(reference, "id"), RequiredString(reference, "name"));
    }

    private static long? OptionalParentIssueId(JsonElement element)
    {
        if (!element.TryGetProperty("parent", out var parent) || parent.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (parent.ValueKind != JsonValueKind.Object)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return RequiredPositiveId(parent, "id");
    }

    private static JsonElement RequiredArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return value;
    }

    private static JsonElement RequiredObject(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return value;
    }

    private static JsonElement OptionalArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            using var empty = JsonDocument.Parse("[]");
            return empty.RootElement.Clone();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return value;
    }

    private static long RequiredPositiveId(JsonElement element, string name)
    {
        var id = RequiredInt64(element, name);
        if (id <= 0)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return id;
    }

    private static long? OptionalPositiveId(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!value.TryGetInt64(out var id) || id <= 0)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return id;
    }

    private static long RequiredNonNegativeLong(JsonElement element, string name)
    {
        var value = RequiredInt64(element, name);
        if (value < 0)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return value;
    }

    private static long RequiredInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return parsed;
    }

    private static int RequiredNonNegativeInt(JsonElement element, string name)
    {
        var value = RequiredInt64(element, name);
        if (value < 0 || value > int.MaxValue)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return (int)value;
    }

    private static int RequiredInt(JsonElement element, string name)
    {
        var value = RequiredInt64(element, name);
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return (int)value;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return value.GetString();
    }

    private static bool RequiredBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return ReadBoolean(value);
    }

    private static bool? OptionalBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return ReadBoolean(value);
    }

    private static bool ReadBoolean(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw RedmineMcpFailure.InvalidUpstream(),
    };

    private static decimal? OptionalDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var parsed))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return parsed;
    }

    private static string RequiredTimestamp(JsonElement element, string name) =>
        ParseTimestamp(RequiredString(element, name));

    private static string? OptionalTimestamp(JsonElement element, string name)
    {
        var value = OptionalString(element, name);
        return value is null ? null : ParseTimestamp(value);
    }

    private static string ParseTimestamp(string value)
    {
        if (!value.Contains('T', StringComparison.Ordinal) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    }

    private static string? OptionalDateOnly(JsonElement element, string name)
    {
        var value = OptionalString(element, name);
        if (value is null)
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }

        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw RedmineMcpFailure.InvalidUpstream();
        }
    }
}
