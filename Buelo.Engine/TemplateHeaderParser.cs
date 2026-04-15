using System.Text;
using System.Text.RegularExpressions;
using Buelo.Contracts;

namespace Buelo.Engine;

/// <summary>
/// Parses DSL header directives (<c>@data</c>, <c>@settings</c>, <c>@schema</c>, <c>@helper</c>)
/// from the top of a Sections-mode template source.
/// <para>
/// Scanning proceeds line-by-line from the top and stops when the first non-directive,
/// non-blank line is encountered.  Recognized directives are stripped from the returned
/// source; unrecognized <c>@</c>-prefixed lines are kept as-is without throwing.
/// <c>@import</c> lines are catalogued in <see cref="TemplateHeader.ImportRefs"/> but
/// <strong>retained</strong> in the stripped source so that <see cref="SectionsTemplateParser"/>
/// can continue to resolve them.
/// </para>
/// </summary>
public static class TemplateHeaderParser
{
    private static readonly Regex DataRegex = new(
        @"^\s*@data\s+from\s+""([^""]+)""\s*$",
        RegexOptions.Compiled);

    private static readonly Regex SettingsSingleLineRegex = new(
        @"^\s*@settings\s*\{([^}]*)\}\s*$",
        RegexOptions.Compiled);

    private static readonly Regex SchemaRegex = new(
        @"^\s*@schema\s+(record\s+\w+\([^)]*\)\s*;?)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex HelperRegex = new(
        @"^\s*@helper\s+(\w+)\(([^)]*)\)\s*=>\s*(.+?)\s*;?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ImportRegex = new(
        @"^\s*@import\s+(header|footer|content)\s+from\s+""([^""]+)""\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses header directives from the top of <paramref name="source"/> and returns
    /// a <see cref="TemplateHeader"/> plus the source with non-import directive lines removed.
    /// </summary>
    public static (TemplateHeader Header, string StrippedSource) Parse(string source)
    {
        if (string.IsNullOrEmpty(source))
            return (new TemplateHeader(), source);

        string? dataRef = null;
        TemplateHeaderSettings? settings = null;
        string? schemaInline = null;
        var importRefs = new List<string>();
        var helpers = new List<TemplateHeaderHelper>();

        var outputLines = new List<string>();
        bool headerDone = false;

        using var reader = new StringReader(source);
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (headerDone)
            {
                outputLines.Add(line);
                continue;
            }

            var trimmed = line.TrimStart();

            // Blank lines are allowed in the header section.
            if (trimmed.Length == 0)
            {
                outputLines.Add(line);
                continue;
            }

            // First non-directive, non-blank line ends the header.
            if (!trimmed.StartsWith('@'))
            {
                headerDone = true;
                outputLines.Add(line);
                continue;
            }

            // --- @import — kept in source for SectionsTemplateParser, also catalogued ---
            if (ImportRegex.IsMatch(trimmed))
            {
                importRefs.Add(trimmed);
                outputLines.Add(line); // retained
                continue;
            }

            // --- @data from "ref" ---
            var m = DataRegex.Match(trimmed);
            if (m.Success)
            {
                dataRef = m.Groups[1].Value;
                continue; // stripped
            }

            // --- @settings { ... } single-line ---
            m = SettingsSingleLineRegex.Match(trimmed);
            if (m.Success)
            {
                settings = ParseSettings(m.Groups[1].Value);
                continue; // stripped
            }

            // --- @settings { (multi-line) ---
            if (trimmed.StartsWith("@settings", StringComparison.Ordinal) && trimmed.Contains('{') && !trimmed.Contains('}'))
            {
                var sb = new StringBuilder(trimmed);
                while ((line = reader.ReadLine()) is not null)
                {
                    sb.Append(' ').Append(line.Trim());
                    if (line.Contains('}')) break;
                }
                var fullBlock = sb.ToString();
                var innerStart = fullBlock.IndexOf('{') + 1;
                var innerEnd = fullBlock.LastIndexOf('}');
                if (innerStart > 0 && innerEnd > innerStart)
                    settings = ParseSettings(fullBlock[innerStart..innerEnd]);
                continue; // stripped
            }

            // --- @schema record TypeName(...); ---
            m = SchemaRegex.Match(trimmed);
            if (m.Success)
            {
                schemaInline = m.Groups[1].Value.Trim();
                continue; // stripped
            }

            // --- @helper Name(params) => expr; ---
            m = HelperRegex.Match(trimmed);
            if (m.Success)
            {
                helpers.Add(new TemplateHeaderHelper(
                    m.Groups[1].Value,
                    m.Groups[2].Value.Trim(),
                    m.Groups[3].Value.Trim()));
                continue; // stripped
            }

            // Unrecognized @directive — kept in output to avoid silent data loss.
            outputLines.Add(line);
        }

        var header = new TemplateHeader
        {
            DataRef = dataRef,
            Settings = settings,
            SchemaInline = schemaInline,
            ImportRefs = importRefs,
            Helpers = helpers
        };

        return (header, string.Join('\n', outputLines));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static TemplateHeaderSettings ParseSettings(string raw)
    {
        string? size = null;
        string? margin = null;
        string? orientation = null;

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Trim();
            var colon = kv.IndexOf(':');
            if (colon < 0) continue;

            var key = kv[..colon].Trim().ToLowerInvariant();
            var value = kv[(colon + 1)..].Trim();

            switch (key)
            {
                case "size": size = value; break;
                case "margin": margin = value; break;
                case "orientation": orientation = value; break;
            }
        }

        return new TemplateHeaderSettings { Size = size, Margin = margin, Orientation = orientation };
    }
}
