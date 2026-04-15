namespace Buelo.Contracts;

/// <summary>
/// A point-in-time snapshot of a <see cref="TemplateRecord"/>'s source and artefacts.
/// Versions are created automatically on every <see cref="ITemplateStore.SaveAsync"/> call.
/// </summary>
public class TemplateVersion
{
    /// <summary>1-based sequential version number within a template's history.</summary>
    public int Version { get; set; }

    /// <summary>Snapshot of <see cref="TemplateRecord.Template"/> at save time.</summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>Snapshot of <see cref="TemplateRecord.Artefacts"/> at save time.</summary>
    public IList<TemplateArtefact> Artefacts { get; set; } = [];

    /// <summary>UTC timestamp when this version was created.</summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>Reserved for future authentication support. Currently always <c>null</c>.</summary>
    public string? SavedBy { get; set; }
}
