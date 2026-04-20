using Buelo.Contracts;
using Buelo.Engine.BueloDsl;

namespace Buelo.Engine.Renderers;

public class RendererInput
{
    /// <summary>Template source code (Sections-mode C# or .buelo DSL).</summary>
    public string Source { get; set; } = string.Empty;

    public TemplateMode Mode { get; set; }

    /// <summary>Raw data object as received from the caller (typically a JsonElement).</summary>
    public object? RawData { get; set; }

    /// <summary>Resolved page settings after cascade (template → request).</summary>
    public PageSettings PageSettings { get; set; } = new();

    /// <summary>Parsed .buelo AST — available only when Mode == BueloDsl. Null otherwise.</summary>
    public BueloDslDocument? BueloDslDocument { get; set; }

    /// <summary>Format-specific hints (e.g., "excel.sheetName"). Populated from @format directive.</summary>
    public IDictionary<string, string> FormatHints { get; set; } = new Dictionary<string, string>();
}
