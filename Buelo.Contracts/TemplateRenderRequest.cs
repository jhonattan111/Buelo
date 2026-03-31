namespace Buelo.Contracts;

/// <summary>
/// Request body used when rendering a persisted template by its <see cref="TemplateRecord.Id"/>.
/// All properties are optional: when omitted the values defined on the template are used as fallback.
/// </summary>
public class TemplateRenderRequest
{
    /// <summary>
    /// Data to pass to the report.
    /// If <c>null</c>, the template's <see cref="TemplateRecord.MockData"/> is used as fallback.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Override the output file name.
    /// If <c>null</c>, <see cref="TemplateRecord.DefaultFileName"/> is used.
    /// </summary>
    public string? FileName { get; set; }
}
