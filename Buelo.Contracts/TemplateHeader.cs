namespace Buelo.Contracts;

/// <summary>
/// Parsed DSL header directives extracted from the top of a Sections-mode template.
/// </summary>
public record TemplateHeader
{
    /// <summary>Reference to a data source declared via <c>@data from "..."</c>.</summary>
    public string? DataRef { get; init; }

    /// <summary>Page settings declared via <c>@settings { ... }</c>.</summary>
    public TemplateHeaderSettings? Settings { get; init; }

    /// <summary>Inline C# record schema declared via <c>@schema record TypeName(...);</c>.</summary>
    public string? SchemaInline { get; init; }

    /// <summary>
    /// Raw <c>@import</c> directive lines found at the top of the template (for introspection/dependency tracking).
    /// These lines are also retained in the stripped source so <see cref="Buelo.Engine.SectionsTemplateParser"/> can process them.
    /// </summary>
    public IReadOnlyList<string> ImportRefs { get; init; } = [];

    /// <summary>Inline helpers declared via <c>@helper Name(params) => expr;</c>.</summary>
    public IReadOnlyList<TemplateHeaderHelper> Helpers { get; init; } = [];

    /// <summary>
    /// Artefact name declared via <c>@helper from "name"</c>.
    /// The artefact must have extension <c>.helpers.cs</c> and contain one or more
    /// static method bodies that will be compiled into <c>BueloGeneratedHelpers</c>.
    /// </summary>
    public string? HelperArtefactRef { get; init; }

    /// <summary>Returns <c>true</c> when no directives were parsed.</summary>
    public bool IsEmpty =>
        DataRef is null && Settings is null && SchemaInline is null
        && ImportRefs.Count == 0 && Helpers.Count == 0 && HelperArtefactRef is null;
}

/// <summary>
/// Page layout settings extracted from an <c>@settings { ... }</c> directive.
/// </summary>
public record TemplateHeaderSettings
{
    /// <summary>Page size (e.g. "A4", "A3", "Letter").</summary>
    public string? Size { get; init; }

    /// <summary>Margin shorthand (e.g. "2cm", "1in", "20mm"). Applied to both horizontal and vertical margins.</summary>
    public string? Margin { get; init; }

    /// <summary>Page orientation: "Portrait" or "Landscape".</summary>
    public string? Orientation { get; init; }
}

/// <summary>
/// An inline helper declared via <c>@helper Name(params) => expr;</c>.
/// </summary>
public record TemplateHeaderHelper(string Name, string Signature, string Body);
