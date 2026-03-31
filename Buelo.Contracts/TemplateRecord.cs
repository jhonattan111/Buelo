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
    /// Defaults to <see cref="TemplateMode.FullClass"/> (the original behaviour).
    /// </summary>
    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;

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

    /// <summary>UTC timestamp when the template was first saved.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the last modification.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
