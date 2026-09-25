using System.Text.Json;
using System.Text.Json.Nodes;
using Corvus.Text.Json.Validator;

namespace IssueHarbor.Analyzer.Tests;

[TestClass]
public sealed class AnalysisResultSchemaTests
{
    private static readonly string[] ExpectedFieldNames =
    [
        "schema_version",
        "change_type",
        "implementation_complexity",
        "requires_code_change",
        "confidence",
        "complexity_rationale"
    ];

    private static readonly Lazy<JsonSchema> ResultSchema = new(
        () => JsonSchema.FromFile(GetSchemaPath()));

    [TestMethod]
    public void Schema_has_exact_approved_required_field_set_and_rejects_extra_properties()
    {
        using var schemaDocument = JsonDocument.Parse(File.ReadAllText(GetSchemaPath()));
        var root = schemaDocument.RootElement;
        var properties = root.GetProperty("properties"u8)
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        var required = root.GetProperty("required"u8)
            .EnumerateArray()
            .Select(property => property.GetString()!)
            .ToArray();

        CollectionAssert.AreEquivalent(ExpectedFieldNames, properties);
        CollectionAssert.AreEquivalent(ExpectedFieldNames, required);
        Assert.IsFalse(root.GetProperty("additionalProperties"u8).GetBoolean());

        var result = ValidResult();
        result["unexpected"] = "not part of schema v1";
        Assert.IsFalse(IsValid(result));
    }

    [TestMethod]
    [DataRow("bug_fix", "yes", "low", "A bounded correction is required.")]
    [DataRow("enhancement", "yes", "high", "The change spans several components.")]
    [DataRow("configuration_or_usage", "no", "none", "Configuration resolves the issue.")]
    [DataRow("no_change", "no", "none", "No product change is necessary.")]
    [DataRow("clarification_needed", "uncertain", "unknown", "目前的信息不足以确定实现复杂度。")]
    [DataRow("bug_fix", "yes", "medium", "La correction touche plusieurs composants liés.")]
    public void Approved_valid_examples_pass(
        string changeType,
        string requiresCodeChange,
        string complexity,
        string rationale)
    {
        Assert.IsTrue(IsValid(ValidResult(changeType, requiresCodeChange, complexity, rationale)));
    }

    [TestMethod]
    public void Missing_required_property_is_rejected()
    {
        var result = ValidResult();
        result.Remove("confidence");

        Assert.IsFalse(IsValid(result));
    }

    [TestMethod]
    public void Invalid_enum_value_is_rejected()
    {
        var result = ValidResult();
        result["change_type"] = "defect";

        Assert.IsFalse(IsValid(result));
    }

    [TestMethod]
    public void No_code_change_with_medium_complexity_is_rejected()
    {
        Assert.IsFalse(IsValid(ValidResult("bug_fix", "no", "medium")));
    }

    [TestMethod]
    public void Code_change_with_none_complexity_is_rejected()
    {
        Assert.IsFalse(IsValid(ValidResult("bug_fix", "yes", "none")));
    }

    [TestMethod]
    public void Uncertain_code_change_with_non_unknown_complexity_is_rejected()
    {
        Assert.IsFalse(IsValid(ValidResult("clarification_needed", "uncertain", "low")));
    }

    [TestMethod]
    public void Configuration_or_usage_with_code_change_is_rejected()
    {
        Assert.IsFalse(IsValid(ValidResult("configuration_or_usage", "yes", "low")));
    }

    [TestMethod]
    public void No_change_with_non_none_complexity_is_rejected()
    {
        Assert.IsFalse(IsValid(ValidResult("no_change", "no", "high")));
    }

    [TestMethod]
    public void Empty_complexity_rationale_is_rejected()
    {
        Assert.IsFalse(IsValid(ValidResult(rationale: string.Empty)));
    }

    [TestMethod]
    public void Wrong_schema_version_is_rejected()
    {
        var result = ValidResult();
        result["schema_version"] = 2;

        Assert.IsFalse(IsValid(result));
    }

    private static string GetSchemaPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Contracts",
        "analysis-result-v1.schema.json");

    private static bool IsValid(JsonObject result) => ResultSchema.Value.Validate(result.ToJsonString());

    private static JsonObject ValidResult(
        string changeType = "bug_fix",
        string requiresCodeChange = "yes",
        string complexity = "low",
        string rationale = "A bounded code change is required.") => new()
    {
        ["schema_version"] = 1,
        ["change_type"] = changeType,
        ["implementation_complexity"] = complexity,
        ["requires_code_change"] = requiresCodeChange,
        ["confidence"] = "medium",
        ["complexity_rationale"] = rationale
    };
}
