namespace Buelo.Tests.Engine;

/// <summary>
/// Example templates demonstrating the PageSettings configuration system.
/// These templates show how to access and use page configuration inside a report.
/// </summary>
public static class PageSettingsExamples
{
    /// <summary>
    /// Basic Builder template that uses PageSettings from the context.
    /// The page configuration (size, margins, colors, watermark) is applied automatically.
    /// </summary>
    public const string BasicTemplate = @"
        Document.Create(container => 
        {
            container.Page(page => 
            {
                var settings = ctx.PageSettings;
        
                page.Size(GetPageSize(settings.PageSize));
                page.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre);
        
                page.Header()
                    .Text((string)data.name)
                    .SemiBold()
                    .FontSize(36)
                    .FontColor(Colors.Blue.Medium);
        
                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x => 
                    {
                        x.Spacing(20);
                        x.Item().Text(Placeholders.LoremIpsum());
                        x.Item().Image(Placeholders.Image(200, 100));
                    });
        
                page.Footer()
                    .AlignCenter()
                    .Text(x => 
                    {
                        x.Span(""Page "");
                        x.CurrentPageNumber();
                    });
            });
        }).GeneratePdf();

        // Helper method to convert PageSettings size to QuestPDF PageSize
        static PageSize GetPageSize(string size) => size.ToUpper() switch
        {
            ""LETTER"" => PageSizes.Letter,
            ""LEGAL"" => PageSizes.Legal,
            ""A3"" => PageSizes.A3,
            ""A4"" => PageSizes.A4,
            ""A5"" => PageSizes.A5,
            _ => PageSizes.A4
        };
    ";

    /// <summary>
    /// FullClass template with watermark support using PageSettings.
    /// </summary>
    public const string FullClassWithWatermark = @"
    public class Report : IReport
    {
        public byte[] GenerateReport(ReportContext ctx)
        {
            var data = ctx.Data;
            var settings = ctx.PageSettings;
        
            return Document.Create(container => 
            {
                container.Page(page => 
                {
                    page.Size(GetPageSize(settings.PageSize));
                    page.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre);
                
                    // Apply watermark if configured
                    if (!string.IsNullOrEmpty(settings.WatermarkText))
                    {
                        page.Background()
                            .AlignCenter()
                            .AlignMiddle()
                            .Text(settings.WatermarkText)
                            .FontSize(settings.WatermarkFontSize)
                            .Opacity(settings.WatermarkOpacity)
                            .FontColor(ParseColor(settings.WatermarkColor));
                    }
                
                    page.Header()
                        .Text((string)data.name)
                        .SemiBold()
                        .FontSize(36)
                        .FontColor(Colors.Blue.Medium);
                
                    page.Content()
                        .Column(x =>
                        {
                            x.Item().Text((string)data.description);
                        });
                
                    page.Footer()
                        .AlignCenter()
                        .Text($""Generated on {DateTime.Now:yyyy-MM-dd}: Page {ctx\""currentPageNumber\""}"");
                });
            }).GeneratePdf();
        }
    
        private static PageSize GetPageSize(string size) => size.ToUpper() switch
        {
            ""LETTER"" => PageSizes.Letter,
            ""A4"" => PageSizes.A4,
            _ => PageSizes.A4
        };
    
        private static Color ParseColor(string hex)
        {
            // Simple hex color parser
            var color = Colors.Black;
            try
            {
                if (hex.StartsWith(""#""))
                    hex = hex.Substring(1);
            
                if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint argb))
                {
                    color = new Color((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
                }
            }
            catch { }
        
            return color;
        }
    }
    ";

    /// <summary>
    /// Demonstrates different page size presets.
    /// </summary>
    public const string MultiplePagesWithVaryingSettings = @"
Document.Create(container => 
{
    // First page - Letter size with wide margins
    container.Page(page =>
    {
        page.Size(PageSizes.Letter);
        page.Margin(3, 2.5f, Unit.Centimetre);
        
        page.Content().Text(""First Page - Letter Size with Wide Margins"");
    });
    
    // Second page - A4 size with compact margins
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(1.5f, Unit.Centimetre);
        
        page.Content().Text(""Second Page - A4 Size with Compact Margins"");
    });
}).GeneratePdf();
";
}
