namespace Buelo.Contracts;

public class ReportRequest
{
    public string Template { get; set; } = string.Empty;
    public string FileName { get; set; } = "report.pdf";
    public object Data { get; set; } = default!;

}
