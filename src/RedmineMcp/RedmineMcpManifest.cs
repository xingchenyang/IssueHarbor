using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IssueHarbor.RedmineMcp;

/// <summary>
/// The frozen Scope 006 tool surface. Handlers deliberately stop before Redmine
/// access; HTTP behavior belongs to Scope 007.
/// </summary>
public static class RedmineMcpManifest
{
    public const int RedmineSinglePageMaximum = 100;

    private const string DeferredAccessMessage =
        "Redmine access is not implemented in Scope 006; upstream access is deferred to Scope 007.";

    public static IReadOnlyList<McpServerTool> CreateTools() =>
    [
        CreateTool(
            nameof(IssuesList),
            "redmine_issues_list",
            "List lightweight Redmine Issue summaries using exactly one of query_id, issue_ids, or status. " +
            "Allowed status values are open, closed, and all. offset defaults to 0; limit defaults to 100 " +
            "and cannot exceed the Redmine single-page maximum. This discovery tool does not return journals " +
            "or attachment content.",
            IssueListOutputSchema),
        CreateTool(
            nameof(IssueGet),
            "redmine_issue_get",
            "Read the factual context for one Redmine Issue, including core fields, custom fields, journals, " +
            "attachment metadata, relations, and children. Scope 006 declares the contract; upstream access " +
            "is deferred to Scope 007.",
            IssueGetOutputSchema),
        CreateTool(
            nameof(QueriesList),
            "redmine_queries_list",
            "List saved Redmine queries visible to the configured credential. This tool has no business input. " +
            "Scope 006 declares the contract; upstream access is deferred to Scope 007.",
            QueriesListOutputSchema),
        CreateTool(
            nameof(AttachmentGet),
            "redmine_attachment_get",
            "Read one Redmine attachment's metadata and MCP-supported binary/blob content without exposing " +
            "a local temporary-file path. Scope 006 declares the contract; upstream access is deferred to Scope 007.",
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
            throw InvalidArgument("exactly one of query_id, issue_ids, or status must be supplied");
        }

        if (query_id is <= 0)
        {
            throw InvalidArgument("query_id must be a positive integer");
        }

        if (issue_ids is not null && (issue_ids.Length == 0 || issue_ids.Any(id => id <= 0)))
        {
            throw InvalidArgument("issue_ids must contain one or more positive integers");
        }

        if (status is not null && status is not ("open" or "closed" or "all"))
        {
            throw InvalidArgument("status must be open, closed, or all");
        }

        if (offset < 0)
        {
            throw InvalidArgument("offset must be non-negative");
        }

        if (limit is < 1 or > RedmineSinglePageMaximum)
        {
            throw InvalidArgument($"limit must be between 1 and {RedmineSinglePageMaximum}");
        }
    }

    internal static void ValidatePositiveId(long id, string name)
    {
        if (id <= 0)
        {
            throw InvalidArgument($"{name} must be a positive integer");
        }
    }

    internal static object IssuesList(
        [Description("Saved Redmine query identifier. Select exactly one selector.")]
        [Range(1, long.MaxValue)]
        long? query_id = null,
        [Description("Explicit positive Redmine Issue identifiers. Must be non-empty when supplied.")]
        long[]? issue_ids = null,
        [Description("Issue status selector: open, closed, or all.")]
        [AllowedValues("open", "closed", "all")]
        string? status = null,
        [Description("Zero-based result offset.")]
        [Range(0, int.MaxValue)]
        int offset = 0,
        [Description("Maximum page size; must be from 1 through 100.")]
        [Range(1, RedmineSinglePageMaximum)]
        int limit = RedmineSinglePageMaximum)
    {
        ValidateIssueListInputs(query_id, issue_ids, status, offset, limit);
        throw DeferredAccess();
    }

    internal static object IssueGet(
        [Description("Positive integer Redmine Issue identifier.")]
        [Range(1, long.MaxValue)]
        long issue_id)
    {
        ValidatePositiveId(issue_id, "issue_id");
        throw DeferredAccess();
    }

    internal static object QueriesList() => throw DeferredAccess();

    internal static object AttachmentGet(
        [Description("Positive integer Redmine attachment identifier.")]
        [Range(1, long.MaxValue)]
        long attachment_id)
    {
        ValidatePositiveId(attachment_id, "attachment_id");
        throw DeferredAccess();
    }

    private static McpServerTool CreateTool(string methodName, string name, string description, string outputSchema)
    {
        var method = typeof(RedmineMcpManifest).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Manifest handler '{methodName}' was not found.");
        using var schemaDocument = JsonDocument.Parse(outputSchema);

        return McpServerTool.Create(method, target: null, new McpServerToolCreateOptions
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

    private static McpException DeferredAccess() => new(DeferredAccessMessage);

    private static McpException InvalidArgument(string message) => new($"invalid_argument: {message}");

    private const string IssueListOutputSchema = """
        {
          "type": "object",
          "properties": {
            "issues": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "issue_id": { "type": "integer" },
                  "project": { "type": ["object", "null"] },
                  "tracker": { "type": ["object", "null"] },
                  "status": { "type": ["object", "null"] },
                  "priority": { "type": ["object", "null"] },
                  "subject": { "type": "string" },
                  "author": { "type": ["object", "null"] },
                  "assigned_to": { "type": ["object", "null"] },
                  "created_on": { "type": ["string", "null"] },
                  "updated_on": { "type": ["string", "null"] }
                },
                "required": ["issue_id", "project", "tracker", "status", "priority", "subject", "author", "assigned_to", "created_on", "updated_on"],
                "additionalProperties": false
              }
            }
          },
          "required": ["issues"],
          "additionalProperties": false
        }
        """;

    private const string IssueGetOutputSchema = """
        {
          "type": "object",
          "properties": {
            "custom_fields": { "type": "array", "items": { "type": "object" } },
            "journals": { "type": "array", "items": { "type": "object" } },
            "attachments": { "type": "array", "items": { "type": "object" } },
            "relations": { "type": "array", "items": { "type": "object" } },
            "children": { "type": "array", "items": { "type": "object" } }
          },
          "required": ["custom_fields", "journals", "attachments", "relations", "children"],
          "additionalProperties": true
        }
        """;

    private const string QueriesListOutputSchema = """
        {
          "type": "object",
          "properties": {
            "queries": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "integer" },
                  "name": { "type": "string" },
                  "is_public": { "type": ["boolean", "null"] },
                  "project_id": { "type": ["integer", "null"] }
                },
                "required": ["id", "name", "is_public", "project_id"],
                "additionalProperties": false
              }
            }
          },
          "required": ["queries"],
          "additionalProperties": false
        }
        """;

    private const string AttachmentGetOutputSchema = """
        {
          "type": "object",
          "properties": {
            "attachment": {
              "type": "object",
              "properties": {
                "id": { "type": "integer" },
                "filename": { "type": "string" },
                "filesize": { "type": "integer" },
                "content_type": { "type": ["string", "null"] },
                "description": { "type": ["string", "null"] },
                "author": { "type": ["object", "null"] },
                "created_on": { "type": ["string", "null"] }
              },
              "required": ["id", "filename", "filesize", "content_type", "description", "author", "created_on"],
              "additionalProperties": false
            }
          },
          "required": ["attachment"],
          "additionalProperties": false
        }
        """;
}
