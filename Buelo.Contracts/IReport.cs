namespace Buelo.Contracts;

public interface IReport
{
    byte[] GenerateReport(ReportContext context);
}
