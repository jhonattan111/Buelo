namespace Buelo.Contracts;

/// <summary>
/// Represents a named, persisted report template that can be rendered on-demand by its <see cref="Id"/>.
/// </summary>
public class TemplateRecord
{
    /// <summary>Unique identifier for this template.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name for the template.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what this template generates.</summary>
    public string? Description { get; set; }

    /// <summary>The template source code.</summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// How the template string should be interpreted.
    /// Defaults to <see cref="TemplateMode.Sections"/>.
    /// </summary>
    public TemplateMode Mode { get; set; } = TemplateMode.Sections;

    /// <summary>
    /// Optional JSON Schema string that describes the shape of the data expected by this template.
    /// Useful for documentation, validation, and IDE tooling.
    /// </summary>
    public string? DataSchema { get; set; }

    /// <summary>
    /// Optional mock data object used for previewing the template without supplying real data.
    /// Can also be used by automated tests to validate that the template renders without errors.
    /// </summary>
    public object? MockData { get; set; }

    /// <summary>Default file name to use when no explicit name is provided at render time.</summary>
    public string DefaultFileName { get; set; } = "report.pdf";

    /// <summary>
    /// Page configuration settings for PDF layout (size, margins, colors, watermark, etc).
    /// These settings are used when rendering this template.
    /// </summary>
    public PageSettings PageSettings { get; set; } = PageSettings.Default();

    /// <summary>UTC timestamp when the template was first saved.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the last modification.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Named artefacts attached to this template (mock data, schemas, helpers, etc.).</summary>
    public IList<TemplateArtefact> Artefacts { get; set; } = [];
}

/// <summary>
/// A named file-like artefact attached to a <see cref="TemplateRecord"/>.
/// </summary>
public class TemplateArtefact
{
    /// <summary>
    /// Optional relative file path (including directories and extension),
    /// e.g. <c>helpers/tax.helpers.cs</c>. When omitted, stores may derive it
    /// from <see cref="Name"/> + <see cref="Extension"/>.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>Slug-safe name (lowercase, hyphens only), e.g. "mockdata", "schema", "helper-tax".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>File extension including the leading dot, e.g. ".json", ".cs", ".schema.json".</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Text content of the artefact.</summary>
    public string Content { get; set; } = string.Empty;
}
