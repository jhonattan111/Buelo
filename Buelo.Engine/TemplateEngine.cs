using Buelo.Contracts;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Buelo.Engine;

public class TemplateEngine(IHelperRegistry helpers)
{
    private readonly ConcurrentDictionary<string, IReport> _cache = new();

    /// <summary>
    /// Renders a template from a raw string.
    /// </summary>
    public async Task<byte[]> RenderAsync(string template, object data, TemplateMode mode = TemplateMode.FullClass)
    {
        string code = mode == TemplateMode.Builder ? WrapBuilderTemplate(template) : template;

        var hash = ComputeHash(code);

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

            string scriptCode = code + "\nreturn new Report();";

            report = await CSharpScript.EvaluateAsync<IReport>(scriptCode, options);
            _cache[hash] = report;
        }

        ReportContext context = new()
        {
            Data = ConvertToDynamic(data),
            Helpers = helpers,
            Globals = new Dictionary<string, object>()
        };

        return report.GenerateReport(context);
    }

    /// <summary>
    /// Renders a persisted <see cref="TemplateRecord"/> using the supplied data.
    /// The template's <see cref="TemplateRecord.Mode"/> controls how the source is interpreted.
    /// </summary>
    public Task<byte[]> RenderTemplateAsync(TemplateRecord template, object data)
        => RenderAsync(template.Template, data, template.Mode);

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Wraps a Builder-mode expression inside the boilerplate class that implements <see cref="IReport"/>.
    /// Inside the expression the variables <c>ctx</c>, <c>data</c>, and <c>helpers</c> are available.
    /// </summary>
    internal static string WrapBuilderTemplate(string body) => $@"public class Report : IReport
{{
    public byte[] GenerateReport(ReportContext ctx)
    {{
        var data = ctx.Data;
        var helpers = ctx.Helpers;
        return {body};
    }}
}}";

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

