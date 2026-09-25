using System.Text.Json;
using IssueHarbor.RedmineMcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace IssueHarbor.RedmineMcp.Tests;

[TestClass]
public sealed class RedmineMcpManifestTests
{
    private static readonly IReadOnlyDictionary<string, Tool> Tools = RedmineMcpManifest.CreateTools()
        .Select(tool => tool.ProtocolTool)
        .ToDictionary(tool => tool.Name, StringComparer.Ordinal);

    [TestMethod]
    public void RegistersExactlyTheApprovedToolSet()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "redmine_issues_list",
                "redmine_issue_get",
                "redmine_queries_list",
                "redmine_attachment_get",
            },
            Tools.Keys.ToArray());
    }

    [TestMethod]
    public void AllToolsAreDeclaredReadOnlyAndNonDestructive()
    {
        foreach (var tool in Tools.Values)
        {
            Assert.IsNotNull(tool.Annotations, tool.Name);
            Assert.IsTrue(tool.Annotations!.ReadOnlyHint, tool.Name);
            Assert.IsFalse(tool.Annotations.DestructiveHint, tool.Name);
        }
    }

    [TestMethod]
    public void IssueListActualSchemaDeclaresOnlyApprovedBusinessInputs()
    {
        var schema = Tools["redmine_issues_list"].InputSchema;
        var properties = schema.GetProperty("properties");
        CollectionAssert.AreEquivalent(
            new[] { "query_id", "issue_ids", "status", "offset", "limit" },
            properties.EnumerateObject().Select(property => property.Name).ToArray());

        CollectionAssert.AreEquivalent(
            new[] { "open", "closed", "all" },
            properties.GetProperty("status").GetProperty("enum").EnumerateArray()
                .Select(value => value.GetString()).ToArray());

        Assert.AreEqual(1, properties.GetProperty("limit").GetProperty("minimum").GetInt32());
        Assert.AreEqual(RedmineMcpManifest.RedmineSinglePageMaximum,
            properties.GetProperty("limit").GetProperty("maximum").GetInt32());
        Assert.IsTrue(SchemaHasType(properties.GetProperty("query_id"), "integer"));
        Assert.IsTrue(SchemaHasType(properties.GetProperty("issue_ids"), "array"));
    }

    [TestMethod]
    public void OtherToolsDeclareIdentifierOrNoBusinessInputs()
    {
        AssertRequiredIntegerProperty(Tools["redmine_issue_get"].InputSchema, "issue_id");
        AssertRequiredIntegerProperty(Tools["redmine_attachment_get"].InputSchema, "attachment_id");

        var queryProperties = Tools["redmine_queries_list"].InputSchema.GetProperty("properties");
        Assert.AreEqual(0, queryProperties.EnumerateObject().Count());
    }

    [TestMethod]
    public void ToolSchemasDeclareHighLevelStructuredOutputs()
    {
        foreach (var tool in Tools.Values)
        {
            Assert.AreEqual("object", GetOutputSchema(tool).GetProperty("type").GetString(), tool.Name);
        }

        AssertRequiredProperties(GetOutputSchema(Tools["redmine_issues_list"]).GetProperty("properties").GetProperty("issues").GetProperty("items"),
            "issue_id", "project", "tracker", "status", "priority", "subject", "author", "assigned_to", "created_on", "updated_on");
        AssertRequiredProperties(GetOutputSchema(Tools["redmine_issue_get"]),
            "custom_fields", "journals", "attachments", "relations", "children");
        AssertRequiredProperties(GetOutputSchema(Tools["redmine_queries_list"]).GetProperty("properties").GetProperty("queries").GetProperty("items"),
            "id", "name", "is_public", "project_id");
        AssertRequiredProperties(GetOutputSchema(Tools["redmine_attachment_get"]).GetProperty("properties").GetProperty("attachment"),
            "id", "filename", "filesize", "content_type", "description", "author", "created_on");
    }

    [TestMethod]
    public void InputSchemasDoNotExposeCredentialsOrConnectionSettings()
    {
        var forbiddenNames = new[] { "api_key", "password", "username", "authorization", "cookie", "base_url" };
        foreach (var tool in Tools.Values)
        {
            var properties = tool.InputSchema.GetProperty("properties");
            foreach (var property in properties.EnumerateObject())
            {
                Assert.IsFalse(forbiddenNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase),
                    $"{tool.Name} exposes forbidden input '{property.Name}'.");
            }
        }
    }

    [TestMethod]
    public void IssueListAcceptsEachSingleSelectorAndDefaultPagination()
    {
        RedmineMcpManifest.ValidateIssueListInputs(84, null, null, 0, 100);
        RedmineMcpManifest.ValidateIssueListInputs(null, [1605, 22382], null, 0, 100);
        RedmineMcpManifest.ValidateIssueListInputs(null, null, "open", 0, 100);
        RedmineMcpManifest.ValidateIssueListInputs(null, null, "closed", 0, 100);
        RedmineMcpManifest.ValidateIssueListInputs(null, null, "all", 0, 100);
    }

    [TestMethod]
    public void IssueListRejectsNoOrMultipleSelectorsAndEmptyExplicitIds()
    {
        AssertInvalidIssueList(null, null, null, 0, 100);
        AssertInvalidIssueList(84, [1605], null, 0, 100);
        AssertInvalidIssueList(null, [], null, 0, 100);
    }

    [TestMethod]
    public void IssueListRejectsInvalidSelectorValuesAndPagination()
    {
        AssertInvalidIssueList(null, null, "in_progress", 0, 100);
        AssertInvalidIssueList(null, null, "OPEN", 0, 100);
        AssertInvalidIssueList(null, null, "open", -1, 100);
        AssertInvalidIssueList(null, null, "open", 0, 0);
        AssertInvalidIssueList(null, null, "open", 0, RedmineMcpManifest.RedmineSinglePageMaximum + 1);
        AssertInvalidIssueList(0, null, null, 0, 100);
        AssertInvalidIssueList(null, [0], null, 0, 100);
    }

    [TestMethod]
    public void IssueAndAttachmentIdentifiersMustBePositiveIntegers()
    {
        RedmineMcpManifest.ValidatePositiveId(1, "issue_id");
        RedmineMcpManifest.ValidatePositiveId(1, "attachment_id");
        AssertThrowsException<McpException>(() => RedmineMcpManifest.ValidatePositiveId(0, "issue_id"));
        AssertThrowsException<McpException>(() => RedmineMcpManifest.ValidatePositiveId(-1, "attachment_id"));
    }

    [TestMethod]
    public void ManifestInspectionRequiresNoRedmineOrNetworkAccess()
    {
        Assert.AreEqual(4, RedmineMcpManifest.CreateTools().Count);
        Assert.IsTrue(Tools.Values.All(tool => tool.InputSchema.ValueKind == JsonValueKind.Object));
    }

    private static void AssertRequiredIntegerProperty(JsonElement schema, string name)
    {
        Assert.AreEqual("integer", schema.GetProperty("properties").GetProperty(name).GetProperty("type").GetString());
        CollectionAssert.Contains(schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray(), name);
        Assert.AreEqual(1, schema.GetProperty("properties").GetProperty(name).GetProperty("minimum").GetInt32());
    }

    private static void AssertRequiredProperties(JsonElement schema, params string[] names)
    {
        var required = schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray();
        foreach (var name in names)
        {
            CollectionAssert.Contains(required, name);
        }
    }

    private static bool SchemaHasType(JsonElement schema, string expectedType)
    {
        if (schema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String && type.GetString() == expectedType)
            {
                return true;
            }

            if (type.ValueKind == JsonValueKind.Array && type.EnumerateArray()
                .Any(value => value.ValueKind == JsonValueKind.String && value.GetString() == expectedType))
            {
                return true;
            }
        }

        foreach (var unionName in new[] { "anyOf", "oneOf", "allOf" })
        {
            if (schema.TryGetProperty(unionName, out var union) && union.ValueKind == JsonValueKind.Array &&
                union.EnumerateArray().Any(item => SchemaHasType(item, expectedType)))
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertInvalidIssueList(long? queryId, long[]? issueIds, string? status, int offset, int limit)
    {
        var exception = AssertThrowsException<McpException>(() =>
            RedmineMcpManifest.ValidateIssueListInputs(queryId, issueIds, status, offset, limit));
        StringAssert.StartsWith(exception.Message, "invalid_argument:");
    }

    private static JsonElement GetOutputSchema(Tool tool) =>
        tool.OutputSchema ?? throw new AssertFailedException($"{tool.Name} has no output schema.");

    private static TException AssertThrowsException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new AssertFailedException($"Expected {typeof(TException).Name} to be thrown.");
    }
}
