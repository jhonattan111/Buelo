namespace Buelo.Engine.BueloDsl;

public record BueloDslParseError(string Message, int Line, int Column, BueloDslErrorSeverity Severity);

public enum BueloDslErrorSeverity { Error, Warning }

/// <summary>
/// Parses the YAML-like .buelo component DSL into a <see cref="BueloDslDocument"/> AST.
/// <para>
/// The source is scanned in two phases:
/// 1. Directive section (import, @data, @settings) at the top of the file.
/// 2. Component blocks identified by non-indented keyword lines (e.g. "report title:").
/// </para>
/// </summary>
public static class BueloDslParser
{
    // Top-level layout keywords that begin a component block.
    private static readonly HashSet<string> LayoutKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "report title", "report resume",
        "page header", "page footer",
        "header", "footer",
        "header column", "footer column",
        "group header", "group footer",
        "data"
    };

    public static BueloDslDocument Parse(string source) => Parse(source, out _);

    public static BueloDslDocument Parse(string source, out IReadOnlyList<BueloDslParseError> errors)
    {
        var err = new List<BueloDslParseError>();
        var doc = ParseCore(source, err);
        errors = err;
        return doc;
    }

    // ── Core ─────────────────────────────────────────────────────────────────

    private static BueloDslDocument ParseCore(string source, List<BueloDslParseError> errors)
    {
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        int i = 0;

        var imports = new List<BueloDslImport>();
        string? dataRef = null;
        BueloDslSettings? settings = null;
        var formatHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var components = new List<BueloDslComponent>();

        // ── Phase 1: Directives ───────────────────────────────────────────────
        while (i < lines.Length)
        {
            var clean = StripComment(lines[i]);
            var trimmed = clean.Trim();
            if (string.IsNullOrEmpty(trimmed)) { i++; continue; }

            // Skip indented lines (they belong to the previous directive block).
            if (HasLeadingWhitespace(lines[i])) { i++; continue; }

            // Stop at first top-level component keyword.
            if (IsLayoutKeyword(trimmed))
                break;

            if (trimmed.StartsWith("import ", StringComparison.OrdinalIgnoreCase))
            {
                var imp = TryParseImport(trimmed, i + 1, errors);
                if (imp is not null) imports.Add(imp);
            }
            else if (trimmed.StartsWith("@data from", StringComparison.OrdinalIgnoreCase))
            {
                dataRef = ExtractQuotedValue(trimmed);
            }
            else if (string.Equals(trimmed, "@settings", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                var block = CollectIndentedBlock(lines, i, out int consumed);
                settings = ParseSettings(block);
                i += consumed;
                continue;
            }
            else if (string.Equals(trimmed, "@format", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                var block = CollectIndentedBlock(lines, i, out int consumed);
                ParseFormatHints(block, formatHints);
                i += consumed;
                continue;
            }
            else
            {
                // Unrecognized non-indented line in the directive section.
                errors.Add(new BueloDslParseError(
                    $"Unrecognized top-level keyword: '{trimmed}'",
                    i + 1, 1, BueloDslErrorSeverity.Warning));
            }

            i++;
        }

        // ── Phase 2: Component blocks ─────────────────────────────────────────
        while (i < lines.Length)
        {
            var clean = StripComment(lines[i]);
            var trimmed = clean.Trim();
            if (string.IsNullOrEmpty(trimmed)) { i++; continue; }

            if (!HasLeadingWhitespace(lines[i]))
            {
                string? kw = ExtractColonKeyword(trimmed);
                if (kw is not null && LayoutKeywords.Contains(kw))
                {
                    i++;
                    var body = CollectIndentedBlock(lines, i, out int consumed);
                    components.Add(ParseLayoutComponent(kw, body, errors));
                    i += consumed;
                    continue;
                }
                else if (!string.IsNullOrEmpty(trimmed))
                {
                    errors.Add(new BueloDslParseError(
                        $"Unrecognized top-level keyword: '{trimmed}'",
                        i + 1, 1, BueloDslErrorSeverity.Warning));
                }
            }

            i++;
        }

        return new BueloDslDocument(
            new BueloDslDirectives(imports, dataRef, settings,
                formatHints.Count > 0 ? formatHints : null),
            components);
    }

    // ── Layout component ──────────────────────────────────────────────────────

    private static BueloDslLayoutComponent ParseLayoutComponent(
        string keyword, string[] bodyLines, List<BueloDslParseError> errors)
    {
        var children = new List<BueloDslComponent>();
        BueloDslStyle? style = null;

        foreach (var node in ParsePropNodes(bodyLines))
        {
            switch (node.Key.ToLowerInvariant())
            {
                case "text":
                    var tv = UnquoteIfQuoted(node.Value ?? "");
                    ValidateExpressions(tv, node.Line, errors);
                    children.Add(new BueloDslTextComponent(tv, null));
                    break;

                case "style":
                    style = ParseStyle(node.ChildLines);
                    break;

                case "table":
                    children.Add(ParseTableComponent(node.ChildLines, errors));
                    break;

                case "panel":
                case "card":
                    var panelChildren = new List<BueloDslComponent>();
                    BueloDslStyle? panelStyle = null;
                    foreach (var pn in node.Children)
                    {
                        if (pn.Key.Equals("text", StringComparison.OrdinalIgnoreCase))
                        {
                            var ptv = UnquoteIfQuoted(pn.Value ?? "");
                            ValidateExpressions(ptv, pn.Line, errors);
                            panelChildren.Add(new BueloDslTextComponent(ptv, null));
                        }
                        else if (pn.Key.Equals("style", StringComparison.OrdinalIgnoreCase))
                        {
                            panelStyle = ParseStyle(pn.ChildLines);
                        }
                    }
                    children.Add(new BueloDslLayoutComponent(node.Key, panelStyle, panelChildren));
                    break;

                case "image":
                    var imgSrc = node.Children.FirstOrDefault(
                        n => n.Key.Equals("src", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
                    var imgW = node.Children.FirstOrDefault(
                        n => n.Key.Equals("width", StringComparison.OrdinalIgnoreCase))?.Value;
                    var imgH = node.Children.FirstOrDefault(
                        n => n.Key.Equals("height", StringComparison.OrdinalIgnoreCase))?.Value;
                    children.Add(new BueloDslImageComponent(imgSrc, imgW, imgH, null));
                    break;

                case "spacer":
                    children.Add(new BueloDslLayoutComponent("spacer", null, []));
                    break;
            }
        }

        return new BueloDslLayoutComponent(keyword, style, children);
    }

    // ── Table component ───────────────────────────────────────────────────────

    private static BueloDslTableComponent ParseTableComponent(
        string[] lines, List<BueloDslParseError> errors)
    {
        var columns = new List<BueloDslTableColumn>();
        BueloDslComponent? groupHeader = null;
        BueloDslComponent? groupFooter = null;
        bool zebra = false;
        BueloDslStyle? headerStyle = null;

        foreach (var node in ParsePropNodes(lines))
        {
            switch (node.Key.ToLowerInvariant())
            {
                case "columns":
                    foreach (var item in node.Children.Where(n => n.IsListItem))
                    {
                        var field = GetChildValue(item, "field");
                        var label = GetChildValue(item, "label") ?? field;
                        var width = GetChildValue(item, "width");
                        var format = GetChildValue(item, "format");
                        columns.Add(new BueloDslTableColumn(field ?? "", label ?? "", width, format));
                    }
                    break;

                case "group header":
                    groupHeader = ParseLayoutComponent("group header", node.ChildLines, errors);
                    break;

                case "group footer":
                    groupFooter = ParseLayoutComponent("group footer", node.ChildLines, errors);
                    break;

                case "zebra":
                    zebra = string.Equals(node.Value, "true", StringComparison.OrdinalIgnoreCase);
                    break;

                case "headerstyle":
                    headerStyle = ParseStyle(node.ChildLines);
                    break;
            }
        }

        return new BueloDslTableComponent(columns, groupHeader, groupFooter, zebra, headerStyle);
    }

    private static string? GetChildValue(PropNode node, string key) =>
        node.Children.FirstOrDefault(n => n.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

    // ── Style ─────────────────────────────────────────────────────────────────

    private static BueloDslStyle ParseStyle(string[] lines)
    {
        int? fontSize = null;
        bool? bold = null, italic = null;
        string? color = null, bgColor = null, align = null;
        string? padding = null, margin = null, border = null;
        string? width = null, height = null, inherit = null;

        foreach (var node in ParsePropNodes(lines))
        {
            switch (node.Key.ToLowerInvariant())
            {
                case "fontsize": if (int.TryParse(node.Value, out int fs)) fontSize = fs; break;
                case "bold": bold = string.Equals(node.Value, "true", StringComparison.OrdinalIgnoreCase); break;
                case "italic": italic = string.Equals(node.Value, "true", StringComparison.OrdinalIgnoreCase); break;
                case "color": color = UnquoteIfQuoted(node.Value ?? ""); break;
                case "backgroundcolor": bgColor = UnquoteIfQuoted(node.Value ?? ""); break;
                case "align": align = node.Value; break;
                case "padding": padding = node.Value; break;
                case "margin": margin = node.Value; break;
                case "border": border = node.Value; break;
                case "width": width = node.Value; break;
                case "height": height = node.Value; break;
                case "inherit": inherit = node.Value; break;
            }
        }

        return new BueloDslStyle(fontSize, bold, italic, color, bgColor, align,
                                  padding, margin, border, width, height, inherit);
    }

    // ── PropNode tree ─────────────────────────────────────────────────────────

    internal sealed class PropNode
    {
        public string Key { get; init; } = "";
        public string? Value { get; init; }
        public int Line { get; init; }
        public PropNode[] Children { get; init; } = [];
        public string[] ChildLines { get; init; } = [];
        public bool IsListItem { get; init; }
    }

    internal static PropNode[] ParsePropNodes(string[] lines)
    {
        if (lines.Length == 0) return [];

        int baseIndent = DetectBaseIndent(lines);
        if (baseIndent < 0) return [];

        var result = new List<PropNode>();
        int i = 0;

        while (i < lines.Length)
        {
            var raw = lines[i];
            var clean = StripComment(raw);
            if (string.IsNullOrWhiteSpace(clean)) { i++; continue; }

            int indent = CountIndent(clean);
            if (indent < baseIndent) { i++; continue; }
            if (indent > baseIndent) { i++; continue; } // deeper → child of previous

            var trimmed = clean.TrimStart();
            int lineNum = i + 1;

            // YAML list item: starts with "- "
            bool isListItem = trimmed.StartsWith("- ");
            if (isListItem) trimmed = trimmed[2..];

            int ci = trimmed.IndexOf(':');
            if (ci < 0) { i++; continue; }

            var key = trimmed[..ci].Trim();
            var val = trimmed[(ci + 1)..].Trim();
            if (string.IsNullOrEmpty(val)) val = null;

            i++;

            if (isListItem)
            {
                // Collect continuation properties at indent+2
                int contIndent = indent + 2;
                var contLines = new List<string>();
                while (i < lines.Length)
                {
                    var nc = StripComment(lines[i]);
                    if (string.IsNullOrWhiteSpace(nc)) { i++; continue; }
                    if (CountIndent(nc) < contIndent) break;
                    contLines.Add(lines[i]);
                    i++;
                }

                // Build all-item lines: synthetic first prop + continuations
                var allItemLines = new List<string>();
                allItemLines.Add(new string(' ', contIndent) + key + ": " + (val ?? ""));
                allItemLines.AddRange(contLines);
                var itemChildren = ParsePropNodes(allItemLines.ToArray());

                result.Add(new PropNode
                {
                    Key = "[item]",
                    Line = lineNum,
                    Children = itemChildren,
                    ChildLines = allItemLines.ToArray(),
                    IsListItem = true
                });
            }
            else
            {
                // Collect deeper child lines
                var childLines = new List<string>();
                while (i < lines.Length)
                {
                    var nc = StripComment(lines[i]);
                    if (string.IsNullOrWhiteSpace(nc)) { i++; continue; }
                    if (CountIndent(nc) <= indent) break;
                    childLines.Add(lines[i]);
                    i++;
                }

                var childArr = childLines.ToArray();
                var children = childLines.Count > 0 ? ParsePropNodes(childArr) : [];

                result.Add(new PropNode
                {
                    Key = key,
                    Value = val,
                    Line = lineNum,
                    Children = children,
                    ChildLines = childArr,
                    IsListItem = false
                });
            }
        }

        return result.ToArray();
    }

    // ── Directive helpers ─────────────────────────────────────────────────────

    private static BueloDslImport? TryParseImport(string line, int lineNum, List<BueloDslParseError> errors)
    {
        var source = ExtractQuotedValue(line);
        if (source is null) return null;

        IReadOnlyList<string> functions;
        if (line.Contains("* from", StringComparison.OrdinalIgnoreCase))
        {
            functions = []; // wildcard
        }
        else
        {
            int start = line.IndexOf('{');
            int end = line.IndexOf('}');
            if (start < 0 || end < 0) return null;
            functions = line[(start + 1)..end]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return new BueloDslImport(functions, source);
    }

    private static BueloDslSettings ParseSettings(string[] lines)
    {
        string? size = null, orientation = null, margin = null;
        foreach (var line in lines)
        {
            var clean = StripComment(line).Trim();
            if (string.IsNullOrEmpty(clean)) continue;
            int ci = clean.IndexOf(':');
            if (ci < 0) continue;
            var k = clean[..ci].Trim().ToLowerInvariant();
            var v = clean[(ci + 1)..].Trim();
            switch (k)
            {
                case "size": size = v; break;
                case "orientation": orientation = v; break;
                case "margin": margin = v; break;
            }
        }
        return new BueloDslSettings(size, orientation, margin);
    }

    private static void ParseFormatHints(string[] lines, Dictionary<string, string> hints)
    {
        // Parse nested structure like:
        //   excel:
        //     sheetName: Colaboradores
        //     freezeHeader: true
        // → "excel.sheetName" = "Colaboradores", "excel.freezeHeader" = "true"
        string? currentPrefix = null;
        foreach (var line in lines)
        {
            var clean = StripComment(line);
            if (string.IsNullOrEmpty(clean.Trim())) continue;

            bool indented = HasLeadingWhitespace(clean);
            var trimmed = clean.Trim();
            int ci = trimmed.IndexOf(':');
            if (ci < 0) continue;
            var k = trimmed[..ci].Trim();
            var v = trimmed[(ci + 1)..].Trim();

            if (!indented)
            {
                // Top-level namespace key (e.g., "excel:")
                currentPrefix = k.ToLowerInvariant();
            }
            else if (currentPrefix is not null && !string.IsNullOrEmpty(v))
            {
                hints[$"{currentPrefix}.{k}"] = v;
            }
        }
    }

    // ── Expression validation ─────────────────────────────────────────────────

    private static void ValidateExpressions(string text, int lineNum, List<BueloDslParseError> errors)
    {
        int depth = 0;
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '{' && text[i + 1] == '{') { depth++; i++; }
            else if (text[i] == '}' && text[i + 1] == '}')
            {
                depth--;
                i++;
                if (depth < 0)
                {
                    errors.Add(new BueloDslParseError("Unexpected closing '}}' in expression",
                        lineNum, i, BueloDslErrorSeverity.Error));
                    depth = 0;
                }
            }
        }
        if (depth > 0)
        {
            errors.Add(new BueloDslParseError(
                "Unclosed expression '{{' — missing '}}'",
                lineNum, 0, BueloDslErrorSeverity.Error));
        }
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    internal static string StripComment(string line)
    {
        bool inQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuote = !inQuote;
            else if (line[i] == '#' && !inQuote) return line[..i];
        }
        return line;
    }

    private static bool HasLeadingWhitespace(string line) =>
        line.Length > 0 && char.IsWhiteSpace(line[0]);

    private static int CountIndent(string line)
    {
        int count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ') count++;
            else if (ch == '\t') count += 4;
            else break;
        }
        return count;
    }

    private static int DetectBaseIndent(string[] lines)
    {
        foreach (var line in lines)
        {
            var clean = StripComment(line);
            if (!string.IsNullOrWhiteSpace(clean))
                return CountIndent(clean);
        }
        return -1;
    }

    /// <summary>
    /// Returns the keyword before the first <c>:</c> in a non-indented line,
    /// or <c>null</c> when no colon is found.
    /// </summary>
    private static string? ExtractColonKeyword(string trimmedLine)
    {
        int ci = trimmedLine.IndexOf(':');
        if (ci < 0) return null;
        return trimmedLine[..ci].Trim().ToLowerInvariant();
    }

    private static bool IsLayoutKeyword(string trimmedLine)
    {
        var kw = ExtractColonKeyword(trimmedLine);
        return kw is not null && LayoutKeywords.Contains(kw);
    }

    private static string? ExtractQuotedValue(string line)
    {
        int s = line.IndexOf('"');
        if (s < 0) return null;
        int e = line.IndexOf('"', s + 1);
        if (e < 0) return null;
        return line[(s + 1)..e];
    }

    private static string UnquoteIfQuoted(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }

    /// <summary>
    /// Collects all lines starting at <paramref name="startIndex"/> that have greater indentation
    /// than <c>0</c> (i.e. are indented relative to the global top level).
    /// </summary>
    private static string[] CollectIndentedBlock(string[] lines, int startIndex, out int consumed)
    {
        var result = new List<string>();
        consumed = 0;
        int? childIndent = null;

        for (int j = startIndex; j < lines.Length; j++)
        {
            var clean = StripComment(lines[j]);
            if (string.IsNullOrWhiteSpace(clean))
            {
                result.Add(lines[j]);
                consumed++;
                continue;
            }

            int ind = CountIndent(clean);

            if (childIndent is null)
            {
                if (ind == 0) break; // no indented block follows
                childIndent = ind;
            }

            if (ind < childIndent) break; // back at parent level

            result.Add(lines[j]);
            consumed++;
        }

        // Trim trailing blank lines from consumed count
        while (result.Count > 0 && string.IsNullOrWhiteSpace(StripComment(result[^1])))
        {
            result.RemoveAt(result.Count - 1);
            consumed--;
        }

        return result.ToArray();
    }

    // ── Auto-detection ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the source appears to be a BueloDsl document
    /// (first meaningful non-comment line starts with a known layout keyword or
    /// <c>import {</c> / <c>@settings</c>).
    /// </summary>
    public static bool IsBueloDslSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        foreach (var rawLine in source.ReplaceLineEndings("\n").Split('\n'))
        {
            var clean = StripComment(rawLine).Trim();
            if (string.IsNullOrEmpty(clean)) continue;

            // Check for import directive
            if (clean.StartsWith("import {", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("import *", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for @settings without { on same line (BueloDsl style)
            if (clean.Equals("@settings", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for layout component keyword
            return IsLayoutKeyword(clean);
        }
        return false;
    }
}
