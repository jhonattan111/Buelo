using Buelo.Contracts;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;

namespace Buelo.Engine;

public class TemplateEngine
{
    private readonly ConcurrentDictionary<string, IReport> _cache = new();

    public async Task<byte[]> RenderAsync(string template, object data)
    {
        var hash = ComputeHash(template);

        if (!_cache.TryGetValue(hash, out var report))
        {
            ScriptOptions options = ScriptOptions.Default
                .AddReferences(
                    typeof(QuestPDF.Fluent.Document).Assembly,
                    typeof(IReport).Assembly,
                    typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly
                )
                .AddImports(
                    "System",
                    "QuestPDF.Fluent",
                    "QuestPDF.Helpers",
                    "QuestPDF.Infrastructure",
                    "Buelo.Contracts"
                );

            string code = template + "\nreturn new Report();";

            report = await CSharpScript.EvaluateAsync<IReport>(code, options);
            _cache[hash] = report;
        }



        ReportContext context = new()
        {
            Data = ConvertToDynamic(data),
            Helpers = new DefaultHelperRegistry(),
            Globals = new Dictionary<string, object>()
        };

        return report.GenerateReport(context);
    }

    private static string ComputeHash(string input)
    {
        return input.GetHashCode().ToString();
    }

    public static object ConvertToDynamic(object data)
    {
        if (data is JsonElement jsonElement)
        {
            return JsonElementToExpando(jsonElement);
        }

        return data;
    }

    public static object JsonElementToExpando(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var expando = new ExpandoObject() as IDictionary<string, object>;

                foreach (var prop in element.EnumerateObject())
                {
                    expando[prop.Name] = JsonElementToExpando(prop.Value);
                }

                return expando;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                              .Select(JsonElementToExpando)
                              .ToList();

            case JsonValueKind.String:
                if (element.TryGetDateTime(out var dt))
                    return dt;

                return element.GetString()!;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                    return l;

                return element.GetDecimal();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            default:
                return null!;
        }
    }
}

