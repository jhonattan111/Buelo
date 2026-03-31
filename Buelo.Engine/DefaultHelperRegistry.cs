using Buelo.Contracts;

namespace Buelo.Engine;

public class DefaultHelperRegistry : IHelperRegistry
{
    public string FormatCurrency(decimal value) => value.ToString("C");
    public string FormatDate(DateTime date) => date.ToString("dd/MM/yyyy");
}
