using System.Text;
using System.Text.Json;

namespace Buelo.Engine;

/// <summary>
/// Infers C# positional record declarations from a JSON string.
/// The output is suitable for injection into Monaco Editor as an extra
/// read-only model so the Roslyn syntax engine can offer IntelliSense on
/// <c>data.</c> access patterns inside templates.
/// </summary>
public static class JsonTypeInferrer
{
    private const int MaxDepth = 10;

    /// <summary>
    /// Infers C# record declarations from a JSON string.
    /// Returns a multi-line C# source fragment suitable for injection into Monaco
    /// as an extra read-only model (no namespace wrapper needed).
    /// </summary>
    /// <param name="json">Valid JSON string to analyse.</param>
    /// <param name="rootTypeName">Name for the root record type. Defaults to <c>DataModel</c>.</param>
    /// <exception cref="JsonException">Thrown when <paramref name="json"/> is not valid JSON.</exception>
    public static string InferCSharpTypes(string json, string rootTypeName = "DataModel")
    {
        using var doc = JsonDocument.Parse(json);
        var records = new List<string>();
        InferRecord(doc.RootElement, rootTypeName, depth: 0, records);
        return string.Join("\n", records);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static void InferRecord(JsonElement element, string typeName, int depth, List<string> records)
    {
        var parameters = new List<string>();
        foreach (var prop in element.EnumerateObject())
        {
            var csharpName = ToPascalCase(prop.Name);
            var type = InferType(prop.Value, csharpName, depth + 1, records);
            parameters.Add($"{type} {csharpName}");
        }
        records.Add($"public record {typeName}({string.Join(", ", parameters)});");
    }

    private static string InferType(JsonElement element, string propName, int depth, List<string> records)
    {
        if (depth >= MaxDepth)
            return "object?";

        return element.ValueKind switch
        {
            JsonValueKind.Object => InferObjectType(element, propName + "Model", depth, records),
            JsonValueKind.Array => InferArrayType(element, propName, depth, records),
            JsonValueKind.String => "string",
            JsonValueKind.Number => element.TryGetInt64(out _) ? "int" : "double",
            JsonValueKind.True or JsonValueKind.False => "bool",
            _ => "object?"
        };
    }

    private static string InferObjectType(JsonElement element, string typeName, int depth, List<string> records)
    {
        InferRecord(element, typeName, depth, records);
        return typeName;
    }

    private static string InferArrayType(JsonElement element, string propName, int depth, List<string> records)
    {
        var enumerator = element.EnumerateArray();
        if (!enumerator.MoveNext())
            return "object[]";

        var first = enumerator.Current;

        if (first.ValueKind == JsonValueKind.Object)
        {
            var itemTypeName = propName + "Item";
            InferRecord(first, itemTypeName, depth, records);
            return itemTypeName + "[]";
        }

        // Primitive or nested array — derive type from first element
        return InferType(first, propName, depth, records) + "[]";
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder(name.Length);
        bool capitalizeNext = true;
        foreach (char c in name)
        {
            if (c is '_' or '-' or ' ')
            {
                capitalizeNext = true;
                continue;
            }
            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }
        return sb.ToString();
    }
}
