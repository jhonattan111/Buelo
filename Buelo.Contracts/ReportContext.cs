namespace Buelo.Contracts;

public class ReportContext
{
    /// <summary>
    /// The data object provided to the template, usually converted from JSON.
    /// </summary>
    public dynamic Data { get; set; }

    /// <summary>
    /// Formatting helpers available inside templates (e.g., for currency, dates).
    /// </summary>
    public IHelperRegistry Helpers { get; set; }

    /// <summary>
    /// Optional global variables that can be accessed throughout the template.
    /// </summary>
    public IDictionary<string, object>? Globals { get; set; }

    /// <summary>
    /// Page configuration settings for PDF layout (size, margins, colors, watermark, etc).
    /// </summary>
    public PageSettings PageSettings { get; set; } = PageSettings.Default();
}
