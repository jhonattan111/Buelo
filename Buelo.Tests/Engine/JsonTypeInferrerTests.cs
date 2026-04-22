using System.Text.Json;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class JsonTypeInferrerTests
{
    // ── Flat primitives ───────────────────────────────────────────────────────

    [Fact]
    public void FlatObject_AllPrimitiveTypes_EmitsSingleRecord()
    {
        var json = """
            {
              "name": "Alice",
              "age": 30,
              "salary": 1234.56,
              "active": true,
              "notes": null
            }
            """;

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("public record DataModel(", result);
        Assert.Contains("string Name", result);
        Assert.Contains("int Age", result);
        Assert.Contains("double Salary", result);
        Assert.Contains("bool Active", result);
        Assert.Contains("object? Notes", result);
    }

    // ── Nested objects ────────────────────────────────────────────────────────

    [Fact]
    public void NestedObject_EmitsTwoRecords_InnerBeforeOuter()
    {
        var json = """
            {
              "company": "Buelo Corp",
              "address": { "street": "Main St", "zip": "12345" }
            }
            """;

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("public record AddressModel(", result);
        Assert.Contains("string Street", result);
        Assert.Contains("string Zip", result);
        Assert.Contains("public record DataModel(", result);
        Assert.Contains("AddressModel Address", result);

        // Inner record must appear before outer (deepest-first ordering)
        var innerIndex = result.IndexOf("public record AddressModel(", StringComparison.Ordinal);
        var outerIndex = result.IndexOf("public record DataModel(", StringComparison.Ordinal);
        Assert.True(innerIndex < outerIndex, "AddressModel must be declared before DataModel.");
    }

    // ── Array of objects ──────────────────────────────────────────────────────

    [Fact]
    public void ArrayOfObjects_EmitsItemRecordAndArrayProperty()
    {
        var json = """
            {
              "employees": [
                { "id": 1, "name": "Ana", "active": true }
              ],
              "total": 3.14
            }
            """;

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("public record EmployeesItem(", result);
        Assert.Contains("int Id", result);
        Assert.Contains("string Name", result);
        Assert.Contains("bool Active", result);
        Assert.Contains("EmployeesItem[] Employees", result);
        Assert.Contains("double Total", result);
        Assert.Contains("public record DataModel(", result);
    }

    [Fact]
    public void ArrayOfObjects_ItemRecordAppearsBeforeParent()
    {
        var json = """{ "items": [{ "x": 1 }] }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        var itemIndex = result.IndexOf("public record ItemsItem(", StringComparison.Ordinal);
        var rootIndex = result.IndexOf("public record DataModel(", StringComparison.Ordinal);
        Assert.True(itemIndex < rootIndex, "ItemsItem must be declared before DataModel.");
    }

    // ── Empty array ───────────────────────────────────────────────────────────

    [Fact]
    public void EmptyArray_EmitsObjectArrayPropertyType()
    {
        var json = """{ "tags": [] }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("object[] Tags", result);
    }

    // ── Null value ────────────────────────────────────────────────────────────

    [Fact]
    public void NullValue_EmitsObjectNullable()
    {
        var json = """{ "extra": null }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("object? Extra", result);
    }

    // ── Primitive arrays ──────────────────────────────────────────────────────

    [Fact]
    public void ArrayOfStrings_EmitsStringArray()
    {
        var json = """{ "names": ["Alice", "Bob"] }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("string[] Names", result);
    }

    [Fact]
    public void ArrayOfIntegers_EmitsIntArray()
    {
        var json = """{ "counts": [1, 2, 3] }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("int[] Counts", result);
    }

    // ── Custom root type name ─────────────────────────────────────────────────

    [Fact]
    public void CustomRootTypeName_IsUsedInOutput()
    {
        var json = """{ "value": 42 }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json, rootTypeName: "ReportData");

        Assert.Contains("public record ReportData(", result);
        Assert.DoesNotContain("DataModel", result);
    }

    // ── Snake_case / kebab-case keys ──────────────────────────────────────────

    [Fact]
    public void SnakeCaseKey_ConvertedToPascalCase()
    {
        var json = """{ "first_name": "Bob", "last-name": "Smith" }""";

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        Assert.Contains("string FirstName", result);
        Assert.Contains("string LastName", result);
    }

    // ── Depth guard ───────────────────────────────────────────────────────────

    [Fact]
    public void ObjectNestedBeyondMaxDepth_EmitsObjectNullable()
    {
        // Build JSON 11 levels deep: root → a → b → c → d → e → f → g → h → i → j
        // Property "j" is at depth 10 and must become object?
        var json = BuildDeepJson(10);

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        // The IModel record (at depth 9) should have its deep property typed as object?
        Assert.Contains("object? J", result);
        // No JModel should be emitted (recursion stopped)
        Assert.DoesNotContain("record JModel", result);
    }

    [Fact]
    public void ObjectAtExactlyMaxDepth_IsStillObject()
    {
        // Depth 9: root(0)→a(1)→b(2)→c(3)→d(4)→e(5)→f(6)→g(7)→h(8) — depth=9 is fine
        var json = BuildDeepJson(9, innerValue: "\"leaf\"");

        var result = JsonTypeInferrer.InferCSharpTypes(json);

        // Property "i" is a string "leaf" at depth 9 — should still be string
        Assert.Contains("string I", result);
    }

    // ── Invalid JSON ──────────────────────────────────────────────────────────

    [Fact]
    public void InvalidJson_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonTypeInferrer.InferCSharpTypes("{ name: missing_quotes }"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds a chain of nested JSON objects <paramref name="depth"/> levels deep.</summary>
    private static string BuildDeepJson(int depth, string innerValue = "42")
    {
        var json = innerValue;
        for (var i = depth - 1; i >= 0; i--)
        {
            var key = ((char)('a' + i)).ToString();
            json = $"{{\"{key}\":{json}}}";
        }
        return json;
    }
}
