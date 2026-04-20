using Buelo.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Buelo.Engine.BueloDsl;

/// <summary>
/// Orchestrates the BueloDsl pipeline: Parse → Render.
/// </summary>
public class BueloDslEngine
{
    private readonly IHelperRegistry _helpers;

    public BueloDslEngine(IHelperRegistry helpers)
    {
        _helpers = helpers;
    }

    /// <summary>
    /// Parses and renders the BueloDsl <paramref name="source"/> using the
    /// provided <paramref name="context"/>.
    /// </summary>
    public Task<byte[]> RenderAsync(string source, ReportContext context)
    {
        var ast = BueloDslParser.Parse(source, out var errors);
        var parseErrors = errors.Where(e => e.Severity == BueloDslErrorSeverity.Error).ToList();
        if (parseErrors.Count > 0)
            throw new InvalidOperationException(
                $"BueloDsl parse errors: {string.Join("; ", parseErrors.Select(e => e.Message))}");

        var pageSettings = context.PageSettings ?? PageSettings.Default();
        if (ast.Directives.Settings is { } ds)
            pageSettings = ApplyDslSettingsStatic(pageSettings ?? PageSettings.Default(), ds);
        if (ast.Directives.ProjectConfig is { } projectConfig)
            pageSettings = TemplateEngine.ApplyProjectConfigSettings(pageSettings, projectConfig);

        var effectiveContext = new ReportContext
        {
            Data = context.Data,
            Helpers = context.Helpers,
            PageSettings = pageSettings,
            Globals = context.Globals
        };

        var pdf = RenderParsed(ast, effectiveContext);
        return Task.FromResult(pdf);
    }

    internal byte[] RenderParsed(BueloDslDocument ast, ReportContext context)
    {
        var settings = context.PageSettings ?? PageSettings.Default();
        var headers = ast.Components
            .Where(c => c is BueloDslLayoutComponent lc &&
                (string.Equals(lc.ComponentType, "page header", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(lc.ComponentType, "header", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var footers = ast.Components
            .Where(c => c is BueloDslLayoutComponent lc &&
                (string.Equals(lc.ComponentType, "page footer", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(lc.ComponentType, "footer", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var content = ast.Components
            .Where(c => !headers.Contains(c) && !footers.Contains(c))
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size((settings.PageSize ?? "A4").ToUpperInvariant() switch
                {
                    "LETTER" => PageSizes.Letter,
                    "LEGAL" => PageSizes.Legal,
                    "A3" => PageSizes.A3,
                    "A5" => PageSizes.A5,
                    _ => PageSizes.A4
                });
                page.MarginHorizontal(settings.MarginHorizontal, Unit.Centimetre);
                page.MarginVertical(settings.MarginVertical, Unit.Centimetre);
                page.DefaultTextStyle(x => x
                    .FontSize(settings.DefaultFontSize)
                    .FontColor(settings.DefaultTextColor));

                if (!string.IsNullOrWhiteSpace(settings.WatermarkText))
                {
                    page.Background().AlignCenter().AlignMiddle().Text(settings.WatermarkText)
                        .FontSize(settings.WatermarkFontSize)
                        .FontColor(settings.WatermarkColor);
                }

                if (settings.ShowHeader && headers.Count > 0)
                {
                    page.Header().Column(col =>
                    {
                        foreach (var comp in headers)
                            RenderComponent(col, comp, context.Data, context.Helpers);
                    });
                }

                page.Content().Column(col =>
                {
                    foreach (var comp in content)
                        RenderComponent(col, comp, context.Data, context.Helpers);
                });

                if (settings.ShowFooter && footers.Count > 0)
                {
                    page.Footer().Column(col =>
                    {
                        foreach (var comp in footers)
                            RenderComponent(col, comp, context.Data, context.Helpers);
                    });
                }
            });
        }).GeneratePdf();
    }

    private static void RenderComponent(ColumnDescriptor col, BueloDslComponent component, object data, IHelperRegistry helpers)
    {
        switch (component)
        {
            case BueloDslTextComponent text:
                RenderText(col, text, data);
                break;
            case BueloDslLayoutComponent layout:
                foreach (var child in layout.Children)
                    RenderComponent(col, child, data, helpers);
                break;
            case BueloDslTableComponent table:
                RenderTable(col, table, data, helpers);
                break;
            case BueloDslImageComponent image:
                col.Item().Text($"[image: {image.Src}]");
                break;
        }
    }

    private static void RenderText(ColumnDescriptor col, BueloDslTextComponent text, object data)
    {
        var resolved = ResolveTextValue(text.Value, data);
        var item = col.Item();
        if (text.Style?.Align is { } align)
        {
            if (align.Equals("center", StringComparison.OrdinalIgnoreCase)) item = item.AlignCenter();
            else if (align.Equals("right", StringComparison.OrdinalIgnoreCase)) item = item.AlignRight();
            else if (align.Equals("left", StringComparison.OrdinalIgnoreCase)) item = item.AlignLeft();
        }

        if (text.Style is null)
        {
            item.Text(resolved);
            return;
        }

        item.Text(t =>
        {
            var span = t.Span(resolved);
            if (text.Style.FontSize.HasValue) span = span.FontSize(text.Style.FontSize.Value);
            if (text.Style.Bold == true) span = span.Bold();
            if (text.Style.Italic == true) span = span.Italic();
            if (!string.IsNullOrWhiteSpace(text.Style.Color)) span = span.FontColor(text.Style.Color);
        });
    }

    private static void RenderTable(ColumnDescriptor col, BueloDslTableComponent table, object data, IHelperRegistry helpers)
    {
        col.Item().Table(tbl =>
        {
            tbl.ColumnsDefinition(columns =>
            {
                foreach (var _ in table.Columns)
                    columns.RelativeColumn();
            });

            tbl.Header(header =>
            {
                foreach (var column in table.Columns)
                    header.Cell().Text(column.Label);
            });

            foreach (var row in ToRows(data))
            {
                foreach (var column in table.Columns)
                {
                    var raw = GetValue(row, column.Field);
                    tbl.Cell().Text(FormatValue(raw, column.Format, helpers));
                }
            }
        });
    }

    private static IEnumerable<object> ToRows(object data)
    {
        if (data is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    yield return item;
                yield break;
            }
            yield return element;
            yield break;
        }

        if (data is IEnumerable enumerable && data is not string)
        {
            foreach (var item in enumerable)
                if (item is not null) yield return item;
            yield break;
        }

        yield return data;
    }

    private static object? GetValue(object source, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = source;

        foreach (var part in parts)
        {
            if (current is null) return null;

            if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty(part, out var prop))
                    {
                        current = prop;
                        continue;
                    }

                    var matched = element.EnumerateObject()
                        .FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase));
                    current = matched.Value.ValueKind == JsonValueKind.Undefined ? null : matched.Value;
                    continue;
                }
                return null;
            }

            if (current is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue(part, out var value))
                {
                    current = value;
                    continue;
                }

                var kv = dict.FirstOrDefault(k => string.Equals(k.Key, part, StringComparison.OrdinalIgnoreCase));
                current = kv.Equals(default(KeyValuePair<string, object>)) ? null : kv.Value;
                continue;
            }

            var property = current.GetType().GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase));
            current = property?.GetValue(current);
        }

        return current;
    }

    private static string ResolveTextValue(string raw, object data)
    {
        return Regex.Replace(raw, "\\{\\{(.*?)\\}\\}", match =>
        {
            var expr = match.Groups[1].Value.Trim();
            var value = GetValue(data, expr.StartsWith("data.", StringComparison.OrdinalIgnoreCase)
                ? expr[5..]
                : expr);
            if (value is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString() ?? string.Empty,
                    JsonValueKind.Number => je.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => je.ToString()
                };
            }
            return Convert.ToString(value) ?? string.Empty;
        });
    }

    private static string FormatValue(object? value, string? format, IHelperRegistry helpers)
    {
        if (value is JsonElement je)
        {
            value = je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number when je.TryGetDecimal(out var dec) => dec,
                JsonValueKind.Number when je.TryGetDouble(out var dbl) => dbl,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => je.ToString()
            };
        }

        if (string.Equals(format, "currency", StringComparison.OrdinalIgnoreCase))
        {
            if (value is decimal dec) return helpers.FormatCurrency(dec);
            if (decimal.TryParse(Convert.ToString(value), out var parsed)) return helpers.FormatCurrency(parsed);
        }

        if (string.Equals(format, "date", StringComparison.OrdinalIgnoreCase))
        {
            if (value is DateTime dt) return helpers.FormatDate(dt);
            if (DateTime.TryParse(Convert.ToString(value), out var parsed)) return helpers.FormatDate(parsed);
        }

        return Convert.ToString(value) ?? string.Empty;
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
