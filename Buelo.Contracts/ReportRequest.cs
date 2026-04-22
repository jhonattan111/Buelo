namespace Buelo.Contracts;

public class ReportRequest
{
    /// <summary>
    /// The C# template source code that implements <c>IDocument</c>.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Output file name for the rendered report.
    /// </summary>
    public string FileName { get; set; } = "report.pdf";

    /// <summary>
    /// The data object to bind to the template for rendering.
    /// </summary>
    public object Data { get; set; } = default!;

    /// <summary>
    /// How the template string should be interpreted.
    /// Defaults to <see cref="TemplateMode.FullClass"/>.
    /// </summary>
    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;

    /// <summary>
    /// Optional page configuration settings for PDF layout.
    /// If omitted, defaults to A4 with 2cm margins.
    /// </summary>
    public PageSettings? PageSettings { get; set; }
}
