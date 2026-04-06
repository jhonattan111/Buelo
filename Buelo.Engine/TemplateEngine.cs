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
    public async Task<byte[]> RenderAsync(string template, object data, TemplateMode mode = TemplateMode.FullClass, PageSettings? pageSettings = null)
    {
        var effectiveMode = ResolveTemplateMode(template, mode);
        string code = effectiveMode == TemplateMode.Builder ? WrapBuilderTemplate(template) : template;

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
            Globals = new Dictionary<string, object>(),
            PageSettings = pageSettings ?? PageSettings.Default()
        };

        return report.GenerateReport(context);
    }

    /// <summary>
    /// Resolves the mode used by the compiler.
    /// When <paramref name="mode"/> is not explicitly Builder, a lightweight heuristic
    /// enables fluent QuestPDF snippets to be sent without wrapper boilerplate.
    /// </summary>
    private static TemplateMode ResolveTemplateMode(string template, TemplateMode mode)
    {
        if (mode == TemplateMode.Builder)
            return TemplateMode.Builder;

        return IsFullClassTemplate(template) ? TemplateMode.FullClass : TemplateMode.Builder;
    }

    private static bool IsFullClassTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return false;

        var normalized = template.Trim();

        return normalized.Contains(" class ", StringComparison.Ordinal)
               || normalized.StartsWith("class ", StringComparison.Ordinal)
               || normalized.Contains("IReport", StringComparison.Ordinal)
               || normalized.Contains("GenerateReport(", StringComparison.Ordinal);
    }

    /// <summary>
    /// Renders a persisted <see cref="TemplateRecord"/> using the supplied data.
    /// The template's <see cref="TemplateRecord.Mode"/> controls how the source is interpreted.
    /// Optional pageSettings override the template's configured settings; defaults to template settings if not provided.
    /// </summary>
    public Task<byte[]> RenderTemplateAsync(TemplateRecord template, object data, PageSettings? pageSettings = null)
        => RenderAsync(template.Template, data, template.Mode, pageSettings ?? template.PageSettings);

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

