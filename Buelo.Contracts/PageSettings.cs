namespace Buelo.Contracts;

/// <summary>
/// Contains all page configuration settings for PDF generation.
/// These settings are applied to every page in the document.
/// </summary>
public class PageSettings
{
    /// <summary>
    /// Page size (e.g., "A4", "Letter", "Legal").
    /// Defaults to "A4".
    /// </summary>
    public string PageSize { get; set; } = "A4";

    /// <summary>
    /// Left and right margins in centimeters.
    /// Defaults to 2.0.
    /// </summary>
    public float MarginHorizontal { get; set; } = 2.0f;

    /// <summary>
    /// Top and bottom margins in centimeters.
    /// Defaults to 2.0.
    /// </summary>
    public float MarginVertical { get; set; } = 2.0f;

    /// <summary>
    /// Background color of the page (hex format, e.g., "#FFFFFF").
    /// Defaults to white ("#FFFFFF").
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Watermark text that appears on every page.
    /// Set to null or empty string to disable.
    /// </summary>
    public string? WatermarkText { get; set; }

    /// <summary>
    /// Watermark color in hex format (e.g., "#CCCCCC").
    /// Only used if WatermarkText is set.
    /// Defaults to light gray.
    /// </summary>
    public string WatermarkColor { get; set; } = "#CCCCCC";

    /// <summary>
    /// Watermark opacity (0.0 to 1.0).
    /// Only used if WatermarkText is set.
    /// Defaults to 0.3 (very subtle).
    /// </summary>
    public float WatermarkOpacity { get; set; } = 0.3f;

    /// <summary>
    /// Watermark font size in points.
    /// Only used if WatermarkText is set.
    /// Defaults to 60.
    /// </summary>
    public int WatermarkFontSize { get; set; } = 60;

    /// <summary>
    /// Default font size for body text in points.
    /// Defaults to 12.
    /// </summary>
    public int DefaultFontSize { get; set; } = 12;

    /// <summary>
    /// Default text color in hex format (e.g., "#000000").
    /// Defaults to black.
    /// </summary>
    public string DefaultTextColor { get; set; } = "#000000";

    /// <summary>
    /// Enable or disable header on the page.
    /// Defaults to true.
    /// </summary>
    public bool ShowHeader { get; set; } = true;

    /// <summary>
    /// Enable or disable footer on the page.
    /// Defaults to true.
    /// </summary>
    public bool ShowFooter { get; set; } = true;

    /// <summary>
    /// Creates a default instance with standard A4 settings.
    /// </summary>
    public static PageSettings Default() => new();

    /// <summary>
    /// Creates a predefined instance for letter-sized pages with 1-inch margins.
    /// </summary>
    public static PageSettings Letter() => new()
    {
        PageSize = "Letter",
        MarginHorizontal = 2.54f,
        MarginVertical = 2.54f
    };

    /// <summary>
    /// Creates a predefined instance for A4 with tight margins.
    /// </summary>
    public static PageSettings A4Compact() => new()
    {
        PageSize = "A4",
        MarginHorizontal = 1.0f,
        MarginVertical = 1.0f
    };

    /// <summary>
    /// Creates a predefined instance with a subtle watermark.
    /// </summary>
    public static PageSettings WithWatermark(string text) => new()
    {
        WatermarkText = text,
        WatermarkOpacity = 0.2f
    };
}
