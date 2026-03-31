namespace Buelo.Contracts;

public class ReportContext
{
    public dynamic Data { get; set; }
    public IHelperRegistry Helpers { get; set; }
    public IDictionary<string, object>? Globals { get; set; }
}
