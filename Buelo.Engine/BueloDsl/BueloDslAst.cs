namespace Buelo.Engine.BueloDsl;

public record BueloDslDocument(
    BueloDslDirectives Directives,
    IReadOnlyList<BueloDslComponent> Components
);

public record BueloDslDirectives(
    IReadOnlyList<BueloDslImport> Imports,
    string? DataRef,
    BueloDslSettings? Settings,
    BueloDslProjectConfig? ProjectConfig = null,
    IReadOnlyDictionary<string, string>? FormatHints = null
);

/// <summary>
/// Parsed content of the <c>@project</c> directive block in a .buelo file.
/// Overrides the template-record page settings for this specific report.
/// </summary>
public record BueloDslProjectConfig(
    string? PageSize,
    string? Orientation,
    double? MarginHorizontal,
    double? MarginVertical,
    string? BackgroundColor,
    string? DefaultTextColor,
    int? DefaultFontSize,
    bool? ShowHeader,
    bool? ShowFooter,
    string? WatermarkText
);

public record BueloDslImport(
    IReadOnlyList<string> Functions,  // empty list = wildcard import
    string Source
);

public record BueloDslSettings(
    string? Size,
    string? Orientation,
    string? Margin
);

public abstract record BueloDslComponent(string ComponentType);

public record BueloDslLayoutComponent(
    string ComponentType,               // "report title", "page header", etc.
    BueloDslStyle? Style,
    IReadOnlyList<BueloDslComponent> Children
) : BueloDslComponent(ComponentType);

public record BueloDslTextComponent(
    string Value,                       // may contain {{ expressions }}
    BueloDslStyle? Style
) : BueloDslComponent("text");

public record BueloDslImageComponent(
    string Src,
    string? Width,
    string? Height,
    BueloDslStyle? Style
) : BueloDslComponent("image");

public record BueloDslTableComponent(
    IReadOnlyList<BueloDslTableColumn> Columns,
    BueloDslComponent? GroupHeader,
    BueloDslComponent? GroupFooter,
    bool Zebra,
    BueloDslStyle? HeaderStyle
) : BueloDslComponent("table");

public record BueloDslTableColumn(
    string Field,
    string Label,
    string? Width,
    string? Format
);

public record BueloDslStyle(
    int? FontSize,
    bool? Bold,
    bool? Italic,
    string? Color,
    string? BackgroundColor,
    string? Align,
    string? Padding,
    string? Margin,
    string? Border,
    string? Width,
    string? Height,
    string? Inherit
);
