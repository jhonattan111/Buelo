namespace Buelo.Tests.Engine;

/// <summary>
/// Example templates demonstrating the PageSettings configuration system.
/// These templates show how to access and use page configuration inside a report.
/// </summary>
public static class PageSettingsExamples
{
    /// <summary>
    /// Sections template that uses PageSettings from the context.
    /// The page configuration (size, margins, colors, watermark) is applied automatically.
    /// </summary>
    public const string BasicTemplate = @"
page => {
    page.Size(PageSizes.A4);
    page.Margin(2, Unit.Centimetre);
}
page.Header()
    .Text((string)data.name)
    .SemiBold()
    .FontSize(18)
    .FontColor(Colors.Blue.Medium);

page.Content()
    .PaddingVertical(1, Unit.Centimetre)
    .Column(x => {
        x.Spacing(20);
        x.Item().Text(Placeholders.LoremIpsum());
    });

page.Footer()
    .AlignCenter()
    .Text(x => {
        x.Span(""Page "");
        x.CurrentPageNumber();
    });
";
}
