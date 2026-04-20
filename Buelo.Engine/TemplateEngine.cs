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

public class TemplateEngine
{
    private readonly IHelperRegistry _helpers;
    private readonly ITemplateStore? _store;
    private readonly IGlobalArtefactStore? _globalStore;
    private readonly IBueloProjectStore? _projectStore;
    private readonly ConcurrentDictionary<string, IReport> _cache = new();

    /// <summary>
    /// Creates a <see cref="TemplateEngine"/> with an optional template store.
    /// The store is required for resolving <c>@import</c> directives in Sections-mode templates.
    /// </summary>
    public TemplateEngine(IHelperRegistry helpers, ITemplateStore? store = null, IGlobalArtefactStore? globalStore = null, IBueloProjectStore? projectStore = null)
    {
        _helpers = helpers;
        _store = store;
        _globalStore = globalStore;
        _projectStore = projectStore;
    }

    /// <summary>
    /// Merges page settings from three levels: project defaults, per-template overrides, and per-request overrides.
    /// Priority: request > template > project.
    /// </summary>
    internal static PageSettings MergeSettings(PageSettings project, PageSettings? template, PageSettings? request)
        => request ?? template ?? project;

    /// <summary>
    /// Renders a template from a raw string.
    /// Applies the project-level <see cref="PageSettings"/> as the base, overridden by <paramref name="pageSettings"/> if provided.
    /// </summary>
    public async Task<byte[]> RenderAsync(string template, object data, TemplateMode mode = TemplateMode.Sections, PageSettings? pageSettings = null)
    {
        var projectSettings = _projectStore is not null
            ? (await _projectStore.GetAsync()).PageSettings
            : PageSettings.Default();
        var effectiveSettings = MergeSettings(projectSettings, null, pageSettings);
        return await RenderCoreAsync(template, data, mode, effectiveSettings, helperPreamble: null);
    }

    /// <summary>
    /// Renders a persisted <see cref="TemplateRecord"/> using the supplied data.
    /// The template's <see cref="TemplateRecord.Mode"/> controls how the source is interpreted.
    /// Optional <paramref name="pageSettings"/> override the template's stored settings.
    /// <para>
    /// When the template header contains a <c>@data from "name"</c> directive the engine
    /// resolves the effective data in this order:
    /// <list type="number">
    ///   <item>An artefact in <see cref="TemplateRecord.Artefacts"/> whose <c>Name</c> (or <c>Name+Extension</c>) matches.</item>
    ///   <item>A cross-template lookup via <see cref="ITemplateStore.GetAsync"/> when the ref parses as a GUID.</item>
    ///   <item>The <paramref name="data"/> argument.</item>
    ///   <item><see cref="TemplateRecord.MockData"/>.</item>
    /// </list>
    /// Throws <see cref="InvalidOperationException"/> only when all four sources yield <c>null</c>.
    /// </para>
    /// <para>
    /// Inline <c>@helper</c> directives and <c>@helper from "artefact"</c> are resolved from
    /// <see cref="TemplateRecord.Artefacts"/> and compiled into a <c>BueloGeneratedHelpers</c>
    /// static class available inside the template body.
    /// </para>
    /// </summary>
    public async Task<byte[]> RenderTemplateAsync(TemplateRecord template, object? data, PageSettings? pageSettings = null)
    {
        object? effectiveData = data;
        string? helperPreamble = null;

        var effectiveMode = ResolveTemplateMode(template.Template, template.Mode);
        if (effectiveMode == TemplateMode.Sections)
        {
            var (header, _) = TemplateHeaderParser.Parse(template.Template);

            // Resolve data.
            if (header.DataRef is { } dataRef)
            {
                var artefact = template.Artefacts.FirstOrDefault(a =>
                    string.Equals(a.Path, dataRef, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Name, dataRef, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{a.Name}{a.Extension}", dataRef, StringComparison.OrdinalIgnoreCase));

                if (artefact is not null)
                {
                    effectiveData = JsonSerializer.Deserialize<JsonElement>(artefact.Content);
                }
                else if (_globalStore is not null)
                {
                    // Global store: try by name (assuming .json extension), then by GUID.
                    var globalArtefact = await _globalStore.GetByNameAsync(dataRef, ".json")
                        ?? (Guid.TryParse(dataRef, out var gaId) ? await _globalStore.GetAsync(gaId) : null);
                    if (globalArtefact is not null)
                        effectiveData = JsonSerializer.Deserialize<JsonElement>(globalArtefact.Content);
                }

                if (effectiveData is null && _store is not null && Guid.TryParse(dataRef, out var refId))
                {
                    var refTemplate = await _store.GetAsync(refId);
                    effectiveData = refTemplate?.MockData;
                }
            }

            // Resolve helpers.
            helperPreamble = await BuildHelperPreambleAsync(header, template.Artefacts, _store, _globalStore);
        }

        effectiveData ??= template.MockData;

        if (effectiveData is null)
            throw new InvalidOperationException(
                "No data available for rendering. Provide data in the request, configure MockData, " +
                "or declare @data from an artefact name in the template header.");

        var projectSettings = _projectStore is not null
            ? (await _projectStore.GetAsync()).PageSettings
            : PageSettings.Default();
        var effectivePageSettings = MergeSettings(projectSettings, template.PageSettings, pageSettings);
        return await RenderCoreAsync(template.Template, effectiveData, template.Mode, effectivePageSettings, helperPreamble);
    }

    // ── Core render pipeline ──────────────────────────────────────────────────

    private async Task<byte[]> RenderCoreAsync(string template, object data, TemplateMode mode, PageSettings? pageSettings, string? helperPreamble)
    {
        var effectiveMode = ResolveTemplateMode(template, mode);

        string source = template;
        PageSettings? effectiveSettings = pageSettings;

        // BueloDsl: parse AST, apply DSL settings, compile to Sections source.
        if (effectiveMode == TemplateMode.BueloDsl)
        {
            var ast = BueloDsl.BueloDslParser.Parse(source);
            if (ast.Directives.Settings is { } ds && effectiveSettings is null)
            {
                effectiveSettings = BueloDsl.BueloDslEngine.ApplyDslSettingsStatic(
                    PageSettings.Default(), ds);
            }
            source = BueloDsl.BueloDslCompiler.Compile(ast, new BueloDsl.CompileOptions());
            effectiveMode = TemplateMode.Sections;
            // Compiled source has no @directives — skip TemplateHeaderParser.
        }
        else if (effectiveMode == TemplateMode.Sections)
        {
            var (header, stripped) = TemplateHeaderParser.Parse(template);
            source = stripped;
            if (header.Settings is { } hs)
                effectiveSettings = ApplyHeaderSettings(effectiveSettings ?? PageSettings.Default(), hs);
        }

        string code = effectiveMode switch
        {
            TemplateMode.Sections => await WrapSectionsTemplateAsync(source, _store, _globalStore),
            TemplateMode.Partial => throw new InvalidOperationException("Partial templates are reusable fragments and cannot be rendered directly."),
            _ => await WrapSectionsTemplateAsync(source, _store, _globalStore)
        };

        // Prepend generated helpers class (Sections mode only).
        if (!string.IsNullOrEmpty(helperPreamble))
            code = helperPreamble + "\n" + code;

        var hash = ComputeHash(code);

        if (!_cache.TryGetValue(hash, out var report))
        {
            string scriptCode = code + "\nreturn new Report();";
            var opts = string.IsNullOrEmpty(helperPreamble)
                ? BuildScriptOptions()
                : BuildScriptOptions().AddImports("static BueloGeneratedHelpers");
            report = await CSharpScript.EvaluateAsync<IReport>(scriptCode, opts);
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
    /// Explicit <see cref="TemplateMode.Partial"/> is respected as-is; everything else resolves to <see cref="TemplateMode.Sections"/>.
    /// </summary>
    internal static TemplateMode ResolveTemplateMode(string template, TemplateMode mode)
    {
        if (mode == TemplateMode.Partial) return TemplateMode.Partial;
        if (mode == TemplateMode.BueloDsl) return TemplateMode.BueloDsl;
        // Auto-detect BueloDsl when mode defaults to Sections.
        if (mode == TemplateMode.Sections && BueloDsl.BueloDslParser.IsBueloDslSource(template))
            return TemplateMode.BueloDsl;
        return TemplateMode.Sections;
    }

    /// <summary>
    /// Compiles <paramref name="template"/> using the same wrapping pipeline as
    /// <see cref="RenderAsync"/> but skips PDF generation.
    /// Returns a <see cref="ValidationResult"/> with <c>Valid = true</c> when the code
    /// compiles without errors, or a list of <see cref="ValidationError"/> items otherwise.
    /// Always returns a result — never throws.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(string template, TemplateMode mode = TemplateMode.Sections)
    {
        var effectiveMode = ResolveTemplateMode(template, mode);

        string code;
        try
        {
            string source = template;
            if (effectiveMode == TemplateMode.BueloDsl)
            {
                var ast = BueloDsl.BueloDslParser.Parse(source);
                source = BueloDsl.BueloDslCompiler.Compile(ast, new BueloDsl.CompileOptions());
                effectiveMode = TemplateMode.Sections;
            }
            else if (effectiveMode == TemplateMode.Sections)
            {
                var (_, stripped) = TemplateHeaderParser.Parse(template);
                source = stripped;
            }

            code = effectiveMode switch
            {
                TemplateMode.Sections => await WrapSectionsTemplateAsync(source, _store, _globalStore),
                TemplateMode.Partial => source,
                _ => await WrapSectionsTemplateAsync(source, _store, _globalStore)
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

    // ── Helper generation ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <c>BueloGeneratedHelpers</c> static class source from inline <c>@helper</c>
    /// directives or from a <c>@helper from "artefact"</c> reference.
    /// Returns <c>null</c> when no helpers are declared.
    /// </summary>
    internal static async Task<string?> BuildHelperPreambleAsync(
        TemplateHeader header,
        IList<TemplateArtefact> artefacts,
        ITemplateStore? store = null,
        IGlobalArtefactStore? globalStore = null)
    {
        // 1. Artefact-based helpers take precedence over inline ones.
        if (header.HelperArtefactRef is { } artefactRef)
        {
            var artefact = artefacts.FirstOrDefault(a =>
                string.Equals(a.Path, artefactRef, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Name, artefactRef, StringComparison.OrdinalIgnoreCase) ||
                string.Equals($"{a.Name}{a.Extension}", artefactRef, StringComparison.OrdinalIgnoreCase));

            if (artefact is not null)
                return WrapHelperClass(artefact.Content);

            // Global store fallback: try by name (.csx), then by GUID.
            if (globalStore is not null)
            {
                var globalArtefact = await globalStore.GetByNameAsync(artefactRef, ".csx")
                    ?? await globalStore.GetByNameAsync(artefactRef, ".cs")
                    ?? (Guid.TryParse(artefactRef, out var gaId) ? await globalStore.GetAsync(gaId) : null);
                if (globalArtefact is not null)
                    return WrapHelperClass(globalArtefact.Content);
            }

            // Cross-template lookup when ref is a GUID.
            if (store is not null && Guid.TryParse(artefactRef, out var refId))
            {
                var refTemplate = await store.GetAsync(refId);
                var crossArtefact = refTemplate?.Artefacts.FirstOrDefault(a =>
                    a.Extension.EndsWith(".helpers.cs", StringComparison.OrdinalIgnoreCase));
                if (crossArtefact is not null)
                    return WrapHelperClass(crossArtefact.Content);
            }
        }

        // 2. Inline @helper directives.
        if (header.Helpers.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var h in header.Helpers)
            {
                // Infer return type as string if signature has parameters, otherwise string.
                // Simpler approach: emit as expression-bodied static method.
                // Signature already contains "Type name" pairs; we need to reconstruct
                // the full method. Sprint spec shows: FormatCNPJ(string value) => expr
                // We stored: Name="FormatCNPJ", Signature="string value", Body="value.Insert(2,\".\")"
                sb.AppendLine($"    public static string {h.Name}({h.Signature}) => {h.Body};");
            }
            return WrapHelperClass(sb.ToString());
        }

        return null;
    }

    private static string WrapHelperClass(string body) =>
        $"public static class BueloGeneratedHelpers\n{{\n{body}\n}}";

    // ── Utilities ─────────────────────────────────────────────────────────────

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
    /// Assembles a Sections-mode source into a compilable <see cref="IReport"/> class.
    /// Resolves any <c>@import</c> directives against <paramref name="store"/> (by GUID first,
    /// then by name).  If the store is <c>null</c> or a target cannot be found, the import
    /// is silently skipped and the inline block for that slot is used instead.
    /// When no inline page-configuration block is present, a fallback using
    /// <c>ctx.PageSettings</c> is emitted automatically.
    /// </summary>
    internal static async Task<string> WrapSectionsTemplateAsync(string source, ITemplateStore? store, IGlobalArtefactStore? globalStore = null)
    {
        var imports = SectionsTemplateParser.ParseImports(source);

        // Resolve imported partial bodies keyed by slot.
        var importedBodies = new Dictionary<SectionSlot, string>();
        foreach (var import in imports)
        {
            // 1. Local template store.
            if (store != null)
            {
                var partial = await ResolvePartialAsync(import.Target, store);
                if (partial != null)
                {
                    importedBodies[import.Slot] = partial.Template;
                    continue;
                }
            }

            // 2. Global artefact store: by name (.buelo) or by GUID.
            if (globalStore != null)
            {
                var globalArtefact = await globalStore.GetByNameAsync(import.Target, ".buelo")
                    ?? (Guid.TryParse(import.Target, out var gaId) ? await globalStore.GetAsync(gaId) : null);
                if (globalArtefact != null)
                    importedBodies[import.Slot] = globalArtefact.Content;
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

