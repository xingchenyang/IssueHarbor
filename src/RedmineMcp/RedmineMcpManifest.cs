using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IssueHarbor.RedmineMcp;

/// <summary>The frozen Scope 006 tool surface wired to the Scope 007 read path.</summary>
public static class RedmineMcpManifest
{
    public const int RedmineSinglePageMaximum = 100;

    internal static IReadOnlyList<McpServerTool> CreateTools(RedmineMcpToolHandlers handlers) =>
    [
        CreateTool(
            handlers,
            nameof(RedmineMcpToolHandlers.IssuesList),
            "redmine_issues_list",
            "List one page of lightweight Redmine Issue summaries using exactly one selector: query_id, issue_ids, or status.",
            IssueListOutputSchema),
        CreateTool(
            handlers,
            nameof(RedmineMcpToolHandlers.IssueGet),
            "redmine_issue_get",
            "Read the current factual context of one Redmine Issue, including custom fields, journals, attachment metadata, relations, and children.",
            IssueGetOutputSchema),
        CreateTool(
            handlers,
            nameof(RedmineMcpToolHandlers.QueriesList),
            "redmine_queries_list",
            "List all saved Redmine queries visible to the configured credential. This tool has no business input.",
            QueriesListOutputSchema),
        CreateTool(
            handlers,
            nameof(RedmineMcpToolHandlers.AttachmentGet),
            "redmine_attachment_get",
            "Read one Redmine attachment's metadata and same-origin binary content as an MCP embedded blob resource.",
            AttachmentGetOutputSchema),
    ];

    internal static void ValidateIssueListInputs(
        long? query_id,
        long[]? issue_ids,
        string? status,
        int offset,
        int limit)
    {
        var selectorCount = (query_id.HasValue ? 1 : 0) + (issue_ids is not null ? 1 : 0) + (status is not null ? 1 : 0);
        if (selectorCount != 1)
        {
            throw RedmineMcpFailure.InvalidArgument();
        }

        if (query_id is <= 0 ||
            (issue_ids is not null && (issue_ids.Length is < 1 or > RedmineSinglePageMaximum || issue_ids.Any(id => id <= 0))) ||
            (status is not null && status is not ("open" or "closed" or "all")) ||
            offset < 0 ||
            limit is < 1 or > RedmineSinglePageMaximum)
        {
            throw RedmineMcpFailure.InvalidArgument();
        }
    }

    internal static void ValidatePositiveId(long id)
    {
        if (id <= 0)
        {
            throw RedmineMcpFailure.InvalidArgument();
        }
    }

    internal static void ValidatePositiveId(long id, string _) => ValidatePositiveId(id);

    private static McpServerTool CreateTool(
        RedmineMcpToolHandlers target,
        string methodName,
        string name,
        string description,
        string outputSchema)
    {
        var method = typeof(RedmineMcpToolHandlers).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("An approved MCP tool handler was not found.");
        using var schemaDocument = JsonDocument.Parse(outputSchema);

        return McpServerTool.Create(method, target, new McpServerToolCreateOptions
        {
            Name = name,
            Title = name,
            Description = description,
            ReadOnly = true,
            Destructive = false,
            OpenWorld = true,
            UseStructuredContent = true,
            OutputSchema = schemaDocument.RootElement.Clone(),
        });
    }

    private const string IssueListOutputSchema = """
        {
          "type": "object",
          "properties": {
            "total_count": { "type": "integer", "minimum": 0 },
            "offset": { "type": "integer", "minimum": 0 },
            "limit": { "type": "integer", "minimum": 1, "maximum": 100 },
            "issues": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "issue_id": { "type": "integer", "minimum": 1 },
                  "project": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
                  "tracker": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
                  "status": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
                  "priority": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
                  "subject": { "type": "string" },
                  "author": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
                  "assigned_to": { "type": ["object", "null"], "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
                  "created_on": { "type": "string", "format": "date-time" },
                  "updated_on": { "type": "string", "format": "date-time" }
                },
                "required": ["issue_id", "project", "tracker", "status", "priority", "subject", "author", "assigned_to", "created_on", "updated_on"],
                "additionalProperties": false
              }
            }
          },
          "required": ["total_count", "offset", "limit", "issues"],
          "additionalProperties": false
        }
        """;

    private const string IssueGetOutputSchema = """
        {
          "type": "object",
          "properties": {
            "issue_id": { "type": "integer", "minimum": 1 },
            "project": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "tracker": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "status": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "priority": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "author": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "assigned_to": { "type": ["object", "null"], "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "category": { "type": ["object", "null"], "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "fixed_version": { "type": ["object", "null"], "properties": { "id": { "type": "integer" }, "name": { "type": "string" } }, "required": ["id", "name"], "additionalProperties": false },
            "parent_issue_id": { "type": ["integer", "null"] },
            "subject": { "type": "string" },
            "description": { "type": ["string", "null"] },
            "start_date": { "type": ["string", "null"], "format": "date" },
            "due_date": { "type": ["string", "null"], "format": "date" },
            "done_ratio": { "type": "integer" },
            "is_private": { "type": "boolean" },
            "estimated_hours": { "type": ["number", "null"] },
            "created_on": { "type": "string", "format": "date-time" },
            "updated_on": { "type": "string", "format": "date-time" },
            "closed_on": { "type": ["string", "null"], "format": "date-time" },
            "custom_fields": { "type": "array", "items": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" }, "multiple": { "type": "boolean" }, "values": { "type": "array" } }, "required": ["id", "name", "multiple", "values"], "additionalProperties": false } },
            "journals": { "type": "array", "items": { "type": "object", "properties": { "id": { "type": "integer" }, "user": { "type": ["object", "null"] }, "notes": { "type": ["string", "null"] }, "created_on": { "type": "string", "format": "date-time" }, "updated_on": { "type": ["string", "null"], "format": "date-time" }, "updated_by": { "type": ["object", "null"] }, "private_notes": { "type": ["boolean", "null"] }, "details": { "type": "array", "items": { "type": "object", "properties": { "property": { "type": "string" }, "name": { "type": "string" }, "old_value": { "type": ["string", "array", "null"] }, "new_value": { "type": ["string", "array", "null"] } }, "required": ["property", "name", "old_value", "new_value"], "additionalProperties": false } } }, "required": ["id", "user", "notes", "created_on", "updated_on", "updated_by", "private_notes", "details"], "additionalProperties": false } },
            "attachments": { "type": "array", "items": { "type": "object", "properties": { "id": { "type": "integer" }, "filename": { "type": "string" }, "filesize": { "type": "integer" }, "content_type": { "type": ["string", "null"] }, "description": { "type": ["string", "null"] }, "author": { "type": ["object", "null"] }, "created_on": { "type": ["string", "null"], "format": "date-time" } }, "required": ["id", "filename", "filesize", "content_type", "description", "author", "created_on"], "additionalProperties": false } },
            "relations": { "type": "array", "items": { "type": "object", "properties": { "id": { "type": "integer" }, "relation_type": { "type": "string" }, "issue_id": { "type": "integer" }, "issue_to_id": { "type": "integer" } }, "required": ["id", "relation_type", "issue_id", "issue_to_id"], "additionalProperties": false } },
            "children": { "type": "array", "items": { "type": "object", "properties": { "issue_id": { "type": "integer" }, "tracker": { "type": "object" }, "subject": { "type": "string" }, "children": { "type": "array", "items": { "type": "object" } } }, "required": ["issue_id", "tracker", "subject", "children"], "additionalProperties": false } }
          },
          "required": ["issue_id", "project", "tracker", "status", "priority", "author", "assigned_to", "category", "fixed_version", "parent_issue_id", "subject", "description", "start_date", "due_date", "done_ratio", "is_private", "estimated_hours", "created_on", "updated_on", "closed_on", "custom_fields", "journals", "attachments", "relations", "children"],
          "additionalProperties": false
        }
        """;

    private const string QueriesListOutputSchema = """
        {
          "type": "object",
          "properties": { "queries": { "type": "array", "items": { "type": "object", "properties": { "id": { "type": "integer" }, "name": { "type": "string" }, "is_public": { "type": ["boolean", "null"] }, "project_id": { "type": ["integer", "null"] } }, "required": ["id", "name", "is_public", "project_id"], "additionalProperties": false } } },
          "required": ["queries"],
          "additionalProperties": false
        }
        """;

    private const string AttachmentGetOutputSchema = """
        {
          "type": "object",
          "properties": { "attachment": { "type": "object", "properties": { "id": { "type": "integer" }, "filename": { "type": "string" }, "filesize": { "type": "integer" }, "content_type": { "type": ["string", "null"] }, "description": { "type": ["string", "null"] }, "author": { "type": ["object", "null"] }, "created_on": { "type": ["string", "null"], "format": "date-time" } }, "required": ["id", "filename", "filesize", "content_type", "description", "author", "created_on"], "additionalProperties": false } },
          "required": ["attachment"],
          "additionalProperties": false
        }
        """;
}
