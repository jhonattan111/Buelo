namespace Buelo.Contracts;

public class ReportRequest
{
    /// <summary>
    /// Optional workspace-relative .buelo file path.
    /// When provided, the render pipeline can load template source from the workspace.
    /// </summary>
    public string? TemplatePath { get; set; }

    /// <summary>
    /// Optional workspace-relative JSON file path used as data source override.
    /// </summary>
    public string? DataSourcePath { get; set; }

    public string Template { get; set; } = string.Empty;
    public string FileName { get; set; } = "report.pdf";
    public object Data { get; set; } = default!;

    /// <summary>
    /// How the template string should be interpreted.
    /// Defaults to <see cref="TemplateMode.BueloDsl"/>.
    /// </summary>
    public TemplateMode Mode { get; set; } = TemplateMode.BueloDsl;

    /// <summary>
    /// Optional page configuration settings for PDF layout.
    /// If omitted, defaults to A4 with 2cm margins.
    /// </summary>
    public PageSettings? PageSettings { get; set; }
}
