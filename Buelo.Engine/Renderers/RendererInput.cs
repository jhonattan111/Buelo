using Buelo.Contracts;

namespace Buelo.Engine.Renderers;

public class RendererInput
{
    /// <summary>Template source code (C# class implementing IDocument).</summary>
    public string Source { get; set; } = string.Empty;

    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;

    /// <summary>Raw data object as received from the caller (typically a JsonElement).</summary>
    public object? RawData { get; set; }

    /// <summary>Resolved page settings after cascade (template → request).</summary>
    public PageSettings PageSettings { get; set; } = new();

    /// <summary>Format-specific hints (e.g., "excel.sheetName").</summary>
    public IDictionary<string, string> FormatHints { get; set; } = new Dictionary<string, string>();
}
