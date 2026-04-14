using System.Text.RegularExpressions;

namespace Buelo.Engine;

/// <summary>Identifies which page slot an import or parsed section targets.</summary>
public enum SectionSlot { Header, Footer, Content }

/// <summary>Represents a single <c>@import</c> directive parsed from a Sections-mode template.</summary>
/// <param name="Slot">The page slot to fill with the imported fragment.</param>
/// <param name="Target">Template name or GUID string to resolve from the store.</param>
public record ImportDirective(SectionSlot Slot, string Target);

/// <summary>
/// Parses Sections-mode template source into its constituent blocks:
/// <c>@import</c> directives, page-configuration lambda body, and per-slot content statements.
/// </summary>
public static class SectionsTemplateParser
{
    private static readonly Regex ImportRegex = new(
        @"^\s*@import\s+(header|footer|content)\s+from\s+""([^""]+)""\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns all <c>@import</c> directives found in <paramref name="source"/>.</summary>
    public static IReadOnlyList<ImportDirective> ParseImports(string source)
    {
        var results = new List<ImportDirective>();
        foreach (Match m in ImportRegex.Matches(source))
        {
            var slot = m.Groups[1].Value.ToLowerInvariant() switch
            {
                "header" => SectionSlot.Header,
                "footer" => SectionSlot.Footer,
                _ => SectionSlot.Content
            };
            results.Add(new ImportDirective(slot, m.Groups[2].Value));
        }
        return results;
    }

    /// <summary>Returns <paramref name="source"/> with all <c>@import</c> lines removed.</summary>
    public static string StripDirectives(string source)
        => ImportRegex.Replace(source, string.Empty);

    /// <summary>
    /// Returns the inner body (statements between the braces) of the top-level
    /// <c>page =&gt; { … }</c> page-configuration block, or <c>null</c> when absent.
    /// </summary>
    public static string? ParsePageConfig(string source)
    {
        int arrowIdx = FindTopLevelPageArrow(source);
        if (arrowIdx < 0) return null;

        var (openBrace, closeBrace) = FindBracedBlock(source, arrowIdx + 7 /* len("page =>") */);
        if (openBrace < 0) return null;

        return source[(openBrace + 1)..closeBrace];
    }

    /// <summary>
    /// Returns the full statement for the named <paramref name="slot"/>
    /// (e.g. <c>page.Header()…;</c>), or <c>null</c> if the slot is absent in the source.
    /// </summary>
    public static string? ParseSection(string source, SectionSlot slot)
    {
        string marker = slot switch
        {
            SectionSlot.Header => "page.Header(",
            SectionSlot.Footer => "page.Footer(",
            _ => "page.Content("
        };

        int idx = source.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        int end = FindStatementEnd(source, idx);
        if (end < 0) return null;

        return source[idx..(end + 1)].Trim();
    }

    // ── Heuristic detection ──────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when <paramref name="source"/> looks like a Sections-mode template
    /// (has <c>@import</c> directives, starts with <c>page =&gt;</c>, or starts with a
    /// top-level <c>page.Header/Content/Footer(</c> statement).
    /// </summary>
    internal static bool IsSectionsTemplate(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;

        if (ParseImports(source).Count > 0) return true;

        var stripped = StripDirectives(source).TrimStart();

        return stripped.StartsWith("page =>", StringComparison.Ordinal)
            || stripped.StartsWith("page.Header(", StringComparison.Ordinal)
            || stripped.StartsWith("page.Content(", StringComparison.Ordinal)
            || stripped.StartsWith("page.Footer(", StringComparison.Ordinal);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Locates the character index of <c>page =&gt;</c> that appears at depth 0
    /// (not nested inside braces or parentheses).  Returns -1 when not found.
    /// </summary>
    private static int FindTopLevelPageArrow(string source)
    {
        const string Arrow = "page =>";
        int depth = 0;
        bool inString = false;
        char stringDelimiter = '"';

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == stringDelimiter) inString = false;
                continue;
            }

            if (c == '"' || c == '\'') { inString = true; stringDelimiter = c; continue; }
            if (c == '(' || c == '{') { depth++; continue; }
            if (c == ')' || c == '}') { depth--; continue; }

            if (depth == 0 && source.AsSpan(i).StartsWith(Arrow.AsSpan(), StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Starting from <paramref name="fromIndex"/>, finds the first <c>{</c> and returns
    /// the indices of the matching opening and closing braces.  Returns (-1,-1) when not found.
    /// </summary>
    private static (int open, int close) FindBracedBlock(string source, int fromIndex)
    {
        int openIdx = source.IndexOf('{', fromIndex);
        if (openIdx < 0) return (-1, -1);

        int depth = 0;
        bool inString = false;
        char stringDelimiter = '"';

        for (int i = openIdx; i < source.Length; i++)
        {
            char c = source[i];

            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == stringDelimiter) inString = false;
                continue;
            }

            if (c == '"' || c == '\'') { inString = true; stringDelimiter = c; continue; }
            if (c == '{') depth++;
            if (c == '}') { depth--; if (depth == 0) return (openIdx, i); }
        }

        return (-1, -1);
    }

    /// <summary>
    /// Starting from <paramref name="start"/>, scans forward and returns the index of the
    /// <c>;</c> that terminates the current statement at brace/parenthesis depth 0.
    /// Returns -1 when not found.
    /// </summary>
    private static int FindStatementEnd(string source, int start)
    {
        int depth = 0;
        bool inString = false;
        char stringDelimiter = '"';

        for (int i = start; i < source.Length; i++)
        {
            char c = source[i];

            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == stringDelimiter) inString = false;
                continue;
            }

            if (c == '"' || c == '\'') { inString = true; stringDelimiter = c; continue; }
            if (c == '{' || c == '(') { depth++; continue; }
            if (c == '}' || c == ')') { depth--; continue; }
            if (c == ';' && depth == 0) return i;
        }

        return -1;
    }
}
