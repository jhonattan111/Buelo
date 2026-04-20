using System.Text;

namespace Buelo.Engine.BueloDsl;

public record CompileOptions(
    string? HelperClassName = null   // name of generated helpers class, if any
);

/// <summary>
/// Compiles a <see cref="BueloDslDocument"/> AST into a Sections-mode C# template string
/// that is then passed to the existing <c>TemplateEngine</c> pipeline.
/// </summary>
public static class BueloDslCompiler
{
    // Layout keywords that map to page.Header()
    private static readonly HashSet<string> HeaderSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "page header", "header"
    };

    // Layout keywords that map to page.Footer()
    private static readonly HashSet<string> FooterSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "page footer", "footer"
    };

    public static string Compile(BueloDslDocument document, CompileOptions options)
    {
        var sb = new StringBuilder();

        var headerComps = document.Components
            .Where(c => HeaderSlots.Contains(c.ComponentType)).ToList();

        var contentComps = document.Components
            .Where(c => !HeaderSlots.Contains(c.ComponentType) && !FooterSlots.Contains(c.ComponentType))
            .ToList();

        var footerComps = document.Components
            .Where(c => FooterSlots.Contains(c.ComponentType)).ToList();

        // ── page.Header() ─────────────────────────────────────────────────────
        if (headerComps.Count > 0)
        {
            sb.AppendLine("page.Header().Column(col =>");
            sb.AppendLine("{");
            foreach (var comp in headerComps)
                EmitComponent(sb, comp, "col", "    ", options);
            sb.AppendLine("});");
        }

        // ── page.Content() ────────────────────────────────────────────────────
        if (contentComps.Count > 0)
        {
            // If the only content is a table (inside a "data" block), emit Table directly.
            if (contentComps.Count == 1 && contentComps[0] is BueloDslLayoutComponent lc &&
                string.Equals(lc.ComponentType, "data", StringComparison.OrdinalIgnoreCase) &&
                lc.Children.Count == 1 && lc.Children[0] is BueloDslTableComponent tbl)
            {
                sb.AppendLine("page.Content().Column(col =>");
                sb.AppendLine("{");
                sb.Append("    ");
                EmitTable(sb, tbl, "col", "    ", options);
                sb.AppendLine("});");
            }
            else
            {
                sb.AppendLine("page.Content().Column(col =>");
                sb.AppendLine("{");
                foreach (var comp in contentComps)
                    EmitComponent(sb, comp, "col", "    ", options);
                sb.AppendLine("});");
            }
        }

        // ── page.Footer() ─────────────────────────────────────────────────────
        if (footerComps.Count > 0)
        {
            sb.AppendLine("page.Footer().Column(col =>");
            sb.AppendLine("{");
            foreach (var comp in footerComps)
                EmitComponent(sb, comp, "col", "    ", options);
            sb.AppendLine("});");
        }

        return sb.ToString().TrimEnd();
    }

    // ── Component dispatch ────────────────────────────────────────────────────

    private static void EmitComponent(StringBuilder sb, BueloDslComponent comp,
        string container, string indent, CompileOptions opts)
    {
        switch (comp)
        {
            case BueloDslTextComponent txt:
                EmitText(sb, txt, container, indent);
                break;

            case BueloDslTableComponent tbl:
                sb.Append(indent);
                EmitTable(sb, tbl, container, indent, opts);
                break;

            case BueloDslLayoutComponent layout:
                EmitLayout(sb, layout, container, indent, opts);
                break;
        }
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    private static void EmitText(StringBuilder sb, BueloDslTextComponent txt,
        string container, string indent)
    {
        string expr = CompileTextValue(txt.Value);

        if (txt.Style is null)
        {
            sb.AppendLine($"{indent}{container}.Item().Text({expr});");
        }
        else
        {
            sb.AppendLine($"{indent}{container}.Item().Text(txt =>");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    txt.Span({expr}){BuildStyleChain(txt.Style)};");
            sb.AppendLine($"{indent}}});");
        }
    }

    // ── Layout (panels, nested) ───────────────────────────────────────────────

    private static void EmitLayout(StringBuilder sb, BueloDslLayoutComponent layout,
        string container, string indent, CompileOptions opts)
    {
        switch (layout.ComponentType.ToLowerInvariant())
        {
            case "report title":
            case "report resume":
            case "data":
            case "header column":
            case "footer column":
            case "group header":
            case "group footer":
                // Emit children directly into the parent container
                foreach (var child in layout.Children)
                    EmitComponent(sb, child, container, indent, opts);
                break;

            case "panel":
            case "card":
                var containerVar = UniqueVar("panel");
                sb.AppendLine($"{indent}{container}.Item().Border(1).Padding(4).Column({containerVar} =>");
                sb.AppendLine($"{indent}{{");
                foreach (var child in layout.Children)
                    EmitComponent(sb, child, containerVar, indent + "    ", opts);
                sb.AppendLine($"{indent}}});");
                break;

            case "spacer":
                sb.AppendLine($"{indent}{container}.Item().Height(10);");
                break;

            default:
                // Generic: render children
                foreach (var child in layout.Children)
                    EmitComponent(sb, child, container, indent, opts);
                break;
        }
    }

    // ── Table ─────────────────────────────────────────────────────────────────

    private static void EmitTable(StringBuilder sb, BueloDslTableComponent tbl,
        string container, string indent, CompileOptions opts)
    {
        sb.AppendLine($"{container}.Item().Table(tbl =>");
        sb.AppendLine($"{indent}{{");

        // Column definitions
        if (tbl.Columns.Count > 0)
        {
            sb.AppendLine($"{indent}    tbl.ColumnsDefinition(c =>");
            sb.AppendLine($"{indent}    {{");
            foreach (var col in tbl.Columns)
            {
                if (col.Width is not null && col.Width.EndsWith('%') &&
                    float.TryParse(col.Width[..^1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float pct))
                    sb.AppendLine($"{indent}        c.RelativeColumn({pct / 10:F0});");
                else
                    sb.AppendLine($"{indent}        c.RelativeColumn();");
            }
            sb.AppendLine($"{indent}    }});");
        }

        // Header row
        if (tbl.Columns.Count > 0)
        {
            sb.AppendLine($"{indent}    tbl.Header(hdr =>");
            sb.AppendLine($"{indent}    {{");
            foreach (var col in tbl.Columns)
                sb.AppendLine($"{indent}        hdr.Cell().Text({CsString(col.Label)});");
            sb.AppendLine($"{indent}    }});");
        }

        // Data rows
        sb.AppendLine($"{indent}    var _rows = data as System.Collections.IEnumerable ?? new object[] {{ data }};");
        sb.AppendLine($"{indent}    foreach (var _row in _rows)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        dynamic _d = _row;");
        foreach (var col in tbl.Columns)
        {
            string cellExpr = col.Format?.ToLowerInvariant() switch
            {
                "currency" => $"helpers.FormatCurrency((decimal)_d.{col.Field})",
                "date" => $"((DateTime)_d.{col.Field}).ToString(\"dd/MM/yyyy\")",
                "percent" => $"((double)_d.{col.Field}).ToString(\"P\")",
                _ => $"Convert.ToString((object)_d.{col.Field}) ?? string.Empty"
            };
            sb.AppendLine($"{indent}        tbl.Cell().Text({cellExpr});");
        }
        sb.AppendLine($"{indent}    }}");

        sb.AppendLine($"{indent}}});");
    }

    // ── Expression compilation ────────────────────────────────────────────────

    /// <summary>
    /// Converts a BueloDsl text value (which may contain <c>{{ expr }}</c> expressions)
    /// into a valid C# string literal or interpolated string.
    /// </summary>
    private static string CompileTextValue(string value)
    {
        if (!value.Contains("{{"))
            return CsString(value);

        // Build C# interpolated string
        var sb = new StringBuilder("$\"");
        int i = 0;
        while (i < value.Length)
        {
            if (i + 1 < value.Length && value[i] == '{' && value[i + 1] == '{')
            {
                int end = value.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    // Unclosed — emit literally
                    sb.Append(EscapeForCsInterpolated(value[i..]));
                    break;
                }
                var expr = value[(i + 2)..end].Trim();
                sb.Append('{');
                sb.Append(CompileExpression(expr));
                sb.Append('}');
                i = end + 2;
            }
            else
            {
                sb.Append(EscapeForCsInterpolated(value[i].ToString()));
                i++;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string CompileExpression(string expr)
    {
        if (expr.StartsWith("data.", StringComparison.Ordinal))
            return $"(string)data.{expr[5..]}";

        if (expr.Equals("page", StringComparison.OrdinalIgnoreCase))
            return "ctx.PageSettings.PageSize";

        if (expr.Equals("pageCount", StringComparison.OrdinalIgnoreCase))
            return "\"?\"";

        if (expr.Equals("now", StringComparison.OrdinalIgnoreCase))
            return "DateTime.Now.ToString(\"dd/MM/yyyy\")";

        // Function call: Name(...) → optional helper prefix
        int paren = expr.IndexOf('(');
        if (paren > 0)
        {
            var funcName = expr[..paren];
            var args = expr[(paren + 1)..^1];
            return $"{funcName}({args})";
        }

        return $"Convert.ToString((object){expr}) ?? string.Empty";
    }

    // ── Style chain ───────────────────────────────────────────────────────────

    private static string BuildStyleChain(BueloDslStyle style)
    {
        var sb = new StringBuilder();
        if (style.FontSize.HasValue) sb.Append($".FontSize({style.FontSize.Value})");
        if (style.Bold == true) sb.Append(".Bold()");
        if (style.Italic == true) sb.Append(".Italic()");
        if (style.Color is not null) sb.Append($".FontColor({CsString(style.Color)})");
        if (style.Align is not null)
        {
            sb.Append(style.Align.ToLowerInvariant() switch
            {
                "center" => ".AlignCenter()",
                "right" => ".AlignRight()",
                "justify" => ".AlignJustify()",
                _ => ".AlignLeft()"
            });
        }
        return sb.ToString();
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string CsString(string value) =>
        "@\"" + value.Replace("\"", "\"\"") + "\"";

    private static string EscapeForCsInterpolated(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"")
         .Replace("{", "{{").Replace("}", "}}");

    private static int _varCounter;
    private static string UniqueVar(string prefix) =>
        $"_{prefix}{System.Threading.Interlocked.Increment(ref _varCounter)}";
}
