namespace Buelo.Contracts;

public class ReportRequest
{
    public string Template { get; set; } = string.Empty;
    public string FileName { get; set; } = "report.pdf";
    public object Data { get; set; } = default!;

    /// <summary>
    /// How the template string should be interpreted.
    /// Defaults to <see cref="TemplateMode.FullClass"/> (original behaviour).
    /// Use <see cref="TemplateMode.Builder"/> to send only the return expression
    /// of <c>GenerateReport</c>; the engine will wrap it automatically.
    /// </summary>
    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;

    /// <summary>
    /// Optional page configuration settings for PDF layout.
    /// If omitted, defaults to A4 with 2cm margins.
    /// </summary>
    public PageSettings? PageSettings { get; set; }
}
