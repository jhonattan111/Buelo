using Buelo.Contracts;

namespace Buelo.Engine.BueloDsl;

/// <summary>
/// Orchestrates the BueloDsl pipeline: Parse → Compile → Render via <see cref="TemplateEngine"/>.
/// Can be used standalone (e.g. in tests or directly from client code).
/// </summary>
public class BueloDslEngine
{
    private readonly IHelperRegistry _helpers;

    public BueloDslEngine(IHelperRegistry helpers)
    {
        _helpers = helpers;
    }

    /// <summary>
    /// Parses and compiles the BueloDsl <paramref name="source"/>, then renders it using the
    /// provided <paramref name="context"/>.
    /// </summary>
    public async Task<byte[]> RenderAsync(string source, ReportContext context)
    {
        var ast = BueloDslParser.Parse(source, out var errors);
        var parseErrors = errors.Where(e => e.Severity == BueloDslErrorSeverity.Error).ToList();
        if (parseErrors.Count > 0)
            throw new InvalidOperationException(
                $"BueloDsl parse errors: {string.Join("; ", parseErrors.Select(e => e.Message))}");

        // Apply DSL settings to page settings if not already set
        PageSettings? pageSettings = context.PageSettings;
        if (ast.Directives.Settings is { } ds)
            pageSettings = ApplyDslSettingsStatic(pageSettings ?? PageSettings.Default(), ds);

        string sectionsSource = BueloDslCompiler.Compile(ast, new CompileOptions());

        // Use a fresh TemplateEngine (no circular dep) for the Sections render
        var engine = new TemplateEngine(_helpers);
        return await engine.RenderAsync(sectionsSource, context.Data, TemplateMode.Sections, pageSettings);
    }

    /// <summary>
    /// Validates the BueloDsl <paramref name="source"/> by parsing it and checking structural
    /// correctness. Returns <c>valid: true</c> when no errors are found.
    /// </summary>
    public (bool Valid, IReadOnlyList<BueloDslParseError> Errors) Validate(string source)
    {
        BueloDslParser.Parse(source, out var errors);
        bool valid = !errors.Any(e => e.Severity == BueloDslErrorSeverity.Error);
        return (valid, errors);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PageSettings ApplyDslSettings(PageSettings @base, BueloDslSettings ds) =>
        ApplyDslSettingsStatic(@base, ds);

    internal static PageSettings ApplyDslSettingsStatic(PageSettings @base, BueloDslSettings ds) =>
        new PageSettings
        {
            PageSize = ds.Size ?? @base.PageSize,
            MarginHorizontal = ParseMarginOrDefault(ds.Margin, @base.MarginHorizontal),
            MarginVertical = ParseMarginOrDefault(ds.Margin, @base.MarginVertical),
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

    private static float ParseMarginOrDefault(string? margin, float fallback)
    {
        if (margin is null) return fallback;
        var v = margin.Trim().ToLowerInvariant();
        if (v.EndsWith("cm") && float.TryParse(v[..^2],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var cm)) return cm;
        if (float.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var plain)) return plain;
        return fallback;
    }
}
