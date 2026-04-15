using Buelo.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Buelo.Engine;

// TemplateEngine intentionally references obsolete FullClass/Builder modes to maintain
// backward-compatible runtime support while the deprecation path is in effect.
#pragma warning disable CS0618

public class TemplateEngine
{
    private readonly IHelperRegistry _helpers;
    private readonly ITemplateStore? _store;
    private readonly ConcurrentDictionary<string, IReport> _cache = new();

    /// <summary>
    /// Creates a <see cref="TemplateEngine"/> with an optional template store.
    /// The store is required for resolving <c>@import</c> directives in Sections-mode templates.
    /// </summary>
    public TemplateEngine(IHelperRegistry helpers, ITemplateStore? store = null)
    {
        _helpers = helpers;
        _store = store;
    }

    /// <summary>
    /// Renders a template from a raw string.
    /// </summary>
    public async Task<byte[]> RenderAsync(string template, object data, TemplateMode mode = TemplateMode.FullClass, PageSettings? pageSettings = null)
    {
        var effectiveMode = ResolveTemplateMode(template, mode);

        // For Sections mode, strip header directives and apply any @settings overrides.
        string source = template;
        PageSettings? effectiveSettings = pageSettings;
        if (effectiveMode == TemplateMode.Sections)
        {
            var (header, stripped) = TemplateHeaderParser.Parse(template);
            source = stripped;
            if (header.Settings is { } hs)
                effectiveSettings = ApplyHeaderSettings(effectiveSettings ?? PageSettings.Default(), hs);
        }

        string code = effectiveMode switch
        {
            TemplateMode.Builder => WrapBuilderTemplate(source),
            TemplateMode.Sections => await WrapSectionsTemplateAsync(source, _store),
            TemplateMode.Partial => throw new InvalidOperationException("Partial templates are reusable fragments and cannot be rendered directly."),
            _ => source   // FullClass: use as-is
        };

        var hash = ComputeHash(code);

        if (!_cache.TryGetValue(hash, out var report))
        {
            string scriptCode = code + "\nreturn new Report();";
            report = await CSharpScript.EvaluateAsync<IReport>(scriptCode, BuildScriptOptions());
            _cache[hash] = report;
        }

        ReportContext context = new()
        {
            Data = ConvertToDynamic(data),
            Helpers = _helpers,
            Globals = new Dictionary<string, object>
            {
                ["__pageSettings"] = effectiveSettings ?? PageSettings.Default()
            },
            PageSettings = effectiveSettings ?? PageSettings.Default()
        };

        return report.GenerateReport(context);
    }

    /// <summary>
    /// Resolves the effective <see cref="TemplateMode"/> for a given source string.
    /// Explicit non-default modes are respected as-is; <see cref="TemplateMode.FullClass"/>
    /// triggers auto-detection via lightweight heuristics.
    /// </summary>
    internal static TemplateMode ResolveTemplateMode(string template, TemplateMode mode)
    {
        // Explicit non-auto modes are used as declared.
        if (mode == TemplateMode.Builder || mode == TemplateMode.Sections || mode == TemplateMode.Partial)
            return mode;

        // Auto-detect from source content.
        if (IsFullClassTemplate(template)) return TemplateMode.FullClass;
        if (SectionsTemplateParser.IsSectionsTemplate(template)) return TemplateMode.Sections;
        return TemplateMode.Builder;
    }

    internal static bool IsFullClassTemplate(string template)
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
    /// Optional <paramref name="pageSettings"/> override the template's stored settings.
    /// </summary>
    public Task<byte[]> RenderTemplateAsync(TemplateRecord template, object data, PageSettings? pageSettings = null)
        => RenderAsync(template.Template, data, template.Mode, pageSettings ?? template.PageSettings);

    /// <summary>
    /// Compiles <paramref name="template"/> using the same wrapping pipeline as
    /// <see cref="RenderAsync"/> but skips PDF generation.
    /// Returns a <see cref="ValidationResult"/> with <c>Valid = true</c> when the code
    /// compiles without errors, or a list of <see cref="ValidationError"/> items otherwise.
    /// Always returns a result — never throws.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(string template, TemplateMode mode = TemplateMode.FullClass)
    {
        var effectiveMode = ResolveTemplateMode(template, mode);

        string code;
        try
        {
            string source = template;
            if (effectiveMode == TemplateMode.Sections)
            {
                var (_, stripped) = TemplateHeaderParser.Parse(template);
                source = stripped;
            }

            code = effectiveMode switch
            {
                TemplateMode.Builder => WrapBuilderTemplate(source),
                TemplateMode.Sections => await WrapSectionsTemplateAsync(source, _store),
                TemplateMode.Partial => source,
                _ => source
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult { Valid = false, Errors = [new ValidationError(ex.Message, 0, 0)] };
        }

        try
        {
            var scriptCode = code + "\nreturn new Report();";
            var script = CSharpScript.Create<IReport>(scriptCode, BuildScriptOptions());
            var diagnostics = script.Compile();

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d =>
                {
                    var span = d.Location.GetLineSpan();
                    return new ValidationError(
                        d.GetMessage(),
                        span.StartLinePosition.Line + 1,
                        span.StartLinePosition.Character + 1);
                })
                .ToList();

            return new ValidationResult { Valid = errors.Count == 0, Errors = errors };
        }
        catch (Exception ex)
        {
            return new ValidationResult { Valid = false, Errors = [new ValidationError(ex.Message, 0, 0)] };
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static ScriptOptions BuildScriptOptions() =>
        ScriptOptions.Default
            .AddReferences(
                typeof(QuestPDF.Fluent.Document).Assembly,
                typeof(IReport).Assembly,
                typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly)
            .AddImports(
                "System",
                "QuestPDF.Fluent",
                "QuestPDF.Helpers",
                "QuestPDF.Infrastructure",
                "Buelo.Contracts");

    /// <summary>
    /// Creates a new <see cref="PageSettings"/> by overriding fields from
    /// <paramref name="base"/> with non-null values from <paramref name="hs"/>.
    /// </summary>
    private static PageSettings ApplyHeaderSettings(PageSettings @base, TemplateHeaderSettings hs)
    {
        float? margin = hs.Margin is not null ? ParseMarginCm(hs.Margin) : null;
        return new PageSettings
        {
            PageSize = hs.Size ?? @base.PageSize,
            MarginHorizontal = margin ?? @base.MarginHorizontal,
            MarginVertical = margin ?? @base.MarginVertical,
            BackgroundColor = @base.BackgroundColor,
            WatermarkText = @base.WatermarkText,
            WatermarkColor = @base.WatermarkColor,
            WatermarkOpacity = @base.WatermarkOpacity,
            WatermarkFontSize = @base.WatermarkFontSize,
            DefaultFontSize = @base.DefaultFontSize,
            DefaultTextColor = @base.DefaultTextColor,
            ShowHeader = @base.ShowHeader,
            ShowFooter = @base.ShowFooter
        };
    }

    /// <summary>Parses a CSS-like margin value (e.g. "2cm", "1in", "20mm") into centimetres.</summary>
    private static float ParseMarginCm(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        if (v.EndsWith("cm") && float.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var cm)) return cm;
        if (v.EndsWith("in") && float.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var inch)) return inch * 2.54f;
        if (v.EndsWith("mm") && float.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var mm)) return mm / 10.0f;
        if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain)) return plain;
        return 2.0f;
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

    /// <summary>
    /// Assembles a Sections-mode source into a compilable <see cref="IReport"/> class.
    /// Resolves any <c>@import</c> directives against <paramref name="store"/> (by GUID first,
    /// then by name).  If the store is <c>null</c> or a target cannot be found, the import
    /// is silently skipped and the inline block for that slot is used instead.
    /// When no inline page-configuration block is present, a fallback using
    /// <c>ctx.PageSettings</c> is emitted automatically.
    /// </summary>
    internal static async Task<string> WrapSectionsTemplateAsync(string source, ITemplateStore? store)
    {
        var imports = SectionsTemplateParser.ParseImports(source);

        // Resolve imported partial bodies keyed by slot.
        var importedBodies = new Dictionary<SectionSlot, string>();
        if (store != null)
        {
            foreach (var import in imports)
            {
                var partial = await ResolvePartialAsync(import.Target, store);
                if (partial != null)
                    importedBodies[import.Slot] = partial.Template;
            }
        }

        string stripped = SectionsTemplateParser.StripDirectives(source);
        string? pageConfig = SectionsTemplateParser.ParsePageConfig(stripped);

        // Per-slot: imported body takes precedence over inline block.
        string? GetBodyForSlot(SectionSlot slot)
        {
            if (importedBodies.TryGetValue(slot, out var imported)) return imported;
            return SectionsTemplateParser.ParseSection(stripped, slot);
        }

        string? headerDecorated = DecorateSlot(SectionSlot.Header, GetBodyForSlot(SectionSlot.Header), importedBodies);
        string? contentDecorated = DecorateSlot(SectionSlot.Content, GetBodyForSlot(SectionSlot.Content), importedBodies);
        string? footerDecorated = DecorateSlot(SectionSlot.Footer, GetBodyForSlot(SectionSlot.Footer), importedBodies);

        var sb = new StringBuilder();
        sb.AppendLine("public class Report : IReport");
        sb.AppendLine("{");
        sb.AppendLine("    public byte[] GenerateReport(ReportContext ctx)");
        sb.AppendLine("    {");
        sb.AppendLine("        var data    = ctx.Data;");
        sb.AppendLine("        var helpers = ctx.Helpers;");
        sb.AppendLine("        static PageSize GetPageSize(string size) => size.ToUpper() switch");
        sb.AppendLine("        {");
        sb.AppendLine("            \"LETTER\" => QuestPDF.Helpers.PageSizes.Letter,");
        sb.AppendLine("            \"LEGAL\"  => QuestPDF.Helpers.PageSizes.Legal,");
        sb.AppendLine("            \"A3\"     => QuestPDF.Helpers.PageSizes.A3,");
        sb.AppendLine("            \"A5\"     => QuestPDF.Helpers.PageSizes.A5,");
        sb.AppendLine("            _        => QuestPDF.Helpers.PageSizes.A4");
        sb.AppendLine("        };");
        sb.AppendLine("        return Document.Create(container =>");
        sb.AppendLine("        {");
        sb.AppendLine("            container.Page(page =>");
        sb.AppendLine("            {");

        if (!string.IsNullOrWhiteSpace(pageConfig))
        {
            // Emit user-supplied page config block body.
            foreach (var line in pageConfig.Split('\n'))
                sb.AppendLine($"                {line.TrimEnd()}");
        }
        else
        {
            // Auto-fallback: apply ctx.PageSettings.
            sb.AppendLine("                page.Size(GetPageSize(ctx.PageSettings.PageSize));");
            sb.AppendLine("                page.MarginHorizontal(ctx.PageSettings.MarginHorizontal, Unit.Centimetre);");
            sb.AppendLine("                page.MarginVertical(ctx.PageSettings.MarginVertical, Unit.Centimetre);");
            sb.AppendLine("                page.DefaultTextStyle(x => x.FontSize(ctx.PageSettings.DefaultFontSize));");
        }

        if (headerDecorated != null) sb.AppendLine($"                {headerDecorated}");
        if (contentDecorated != null) sb.AppendLine($"                {contentDecorated}");
        if (footerDecorated != null) sb.AppendLine($"                {footerDecorated}");

        sb.AppendLine("            });");
        sb.AppendLine("        }).GeneratePdf();");
        sb.AppendLine("    }");
        sb.Append("}");

        return sb.ToString();
    }

    /// <summary>
    /// Returns the statement to emit for a slot.
    /// For imported bodies the <c>page.Slot()</c> prefix is added and a trailing <c>;</c>
    /// appended when absent.  For inline parsed sections the statement is returned as-is.
    /// </summary>
    private static string? DecorateSlot(
        SectionSlot slot,
        string? body,
        Dictionary<SectionSlot, string> importedBodies)
    {
        if (body == null) return null;

        if (!importedBodies.ContainsKey(slot))
            return body; // inline: already includes page.Slot()...;

        string prefix = slot switch
        {
            SectionSlot.Header => "page.Header()",
            SectionSlot.Footer => "page.Footer()",
            _ => "page.Content()"
        };

        var trimmed = body.Trim();
        if (!trimmed.EndsWith(';')) trimmed += ';';
        return $"{prefix}\n                    {trimmed}";
    }

    private static async Task<TemplateRecord?> ResolvePartialAsync(string target, ITemplateStore store)
    {
        // Resolve by GUID first.
        if (Guid.TryParse(target, out var id))
            return await store.GetAsync(id);

        // Resolve by name, restricting to Partial records.
        var all = await store.ListAsync();
        return all.FirstOrDefault(t =>
            t.Mode == TemplateMode.Partial &&
            t.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
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

