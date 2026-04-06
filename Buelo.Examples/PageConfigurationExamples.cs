using Buelo.Contracts;

namespace Buelo.Examples;

/// <summary>
/// Exemplo prático: transformar seu código original em um relatório
/// com configurações de página parametrizáveis.
/// </summary>
public static class PageConfigurationExamples
{
    /// <summary>
    /// SEU CÓDIGO ORIGINAL transformado em um template que usa PageSettings.
    /// 
    /// Antes:
    /// Document.Create(container => { 
    ///     container.Page(page => { 
    ///         page.Size(PageSizes.A4);
    ///         page.Margin(2, Unit.Centimetre); 
    ///         page.PageColor(Colors.White); 
    ///         page.DefaultTextStyle(x => x.FontSize(20));  
    ///         page.Header().Text((string)data.name).SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);  
    ///         page.Content().PaddingVertical(1, Unit.Centimetre).Column(x => { 
    ///             x.Spacing(20); 
    ///             x.Item().Text(Placeholders.LoremIpsum()); 
    ///             x.Item().Image(Placeholders.Image(200, 100)); 
    ///         });  
    ///         page.Footer().AlignCenter().Text(x => { 
    ///             x.Span("Page"); 
    ///             x.CurrentPageNumber(); 
    ///         }); 
    ///     }); 
    /// }).GeneratePdf();
    /// 
    /// Depois - Config via PageSettings:
    /// </summary>
    public const string RefactoredTemplate = @"
Document.Create(container => { 
    container.Page(page => { 
        var settings = ctx.PageSettings;
        
        // Tamanho e margens vêm de PageSettings
        page.Size(GetPageSize(settings.PageSize));
        page.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre); 
        page.PageColor(ParseColor(settings.BackgroundColor));
        
        // Fonte padrão vem de PageSettings
        page.DefaultTextStyle(x => x.FontSize(settings.DefaultFontSize));  
        
        // Header
        if (settings.ShowHeader)
        {
            page.Header()
                .Text((string)data.name)
                .SemiBold()
                .FontSize(36)
                .FontColor(Colors.Blue.Medium);
        }
        
        // Content
        page.Content()
            .PaddingVertical(1, Unit.Centimetre)
            .Column(x => { 
                x.Spacing(20); 
                x.Item().Text(Placeholders.LoremIpsum()); 
                x.Item().Image(Placeholders.Image(200, 100)); 
            });  
        
        // Marca d'água (se configurada)
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
        
        // Footer
        if (settings.ShowFooter)
        {
            page.Footer()
                .AlignCenter()
                .Text(x => { 
                    x.Span(""Page ""); 
                    x.CurrentPageNumber(); 
                });
        }
    }); 
}).GeneratePdf();

// Helpers
static PageSize GetPageSize(string size) => size.ToUpper() switch
{
    ""LETTER"" => PageSizes.Letter,
    ""LEGAL"" => PageSizes.Legal,
    ""A3"" => PageSizes.A3,
    ""A4"" => PageSizes.A4,
    ""A5"" => PageSizes.A5,
    _ => PageSizes.A4
};

static Color ParseColor(string hex)
{
    try
    {
        if (hex.StartsWith(""#""))
            hex = hex.Substring(1);
        
        if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint argb))
        {
            return new Color(
                (byte)(argb >> 16), 
                (byte)(argb >> 8), 
                (byte)argb
            );
        }
    }
    catch { }
    
    return Colors.Black;
}
";

    /// <summary>
    /// Criar um template com as configurações que você quer como padrão
    /// </summary>
    public static TemplateRecord CreateYourCustomReport()
    {
        return new TemplateRecord
        {
            Name = "Meu Relatório Personalizado",
            Description = "Relatório com configurações de página totalmente customizáveis",
            Template = RefactoredTemplate,
            Mode = TemplateMode.Builder,
            DefaultFileName = "meu-relatorio.pdf",

            // Configurações de página (agora parametrizáveis!)
            PageSettings = new PageSettings
            {
                PageSize = "A4",
                MarginHorizontal = 2.0f,
                MarginVertical = 2.0f,
                BackgroundColor = "#FFFFFF",
                DefaultFontSize = 20,  // Era hardcoded como 20 antes
                DefaultTextColor = "#000000",
                WatermarkText = null,  // Sem marca d'água por padrão
                ShowHeader = true,
                ShowFooter = true
            },

            // Dados de exemplo para preview
            MockData = new
            {
                name = "Relatório de Exemplo",
                description = "Este é um relatório de teste"
            }
        };
    }

    /// <summary>
    /// Exemplo: Como fazer requisição com configurações customizadas
    /// </summary>
    public static ReportRequest CreateRequestWithCustomSettings()
    {
        return new ReportRequest
        {
            // Seu template (refatorado ou o código original)
            Template = RefactoredTemplate,
            FileName = "relatorio-customizado.pdf",
            Data = new { name = "Meu Dados" },
            Mode = TemplateMode.Builder,

            // NOVIDADE! Configurações de página personalizadas
            PageSettings = new PageSettings
            {
                PageSize = "Letter",           // Mudou de A4 para Letter
                MarginHorizontal = 1.5f,       // Pode customizar margens
                MarginVertical = 1.5f,
                BackgroundColor = "#F5F5F5",   // Fundo diferente
                DefaultFontSize = 12,          // Fonte menor
                DefaultTextColor = "#333333",  // Texto mais escuro
                WatermarkText = "CONFIDENCIAL",// MARCA D'ÁGUA!
                WatermarkColor = "#FF0000",
                WatermarkOpacity = 0.15f,
                WatermarkFontSize = 70,
                ShowHeader = true,
                ShowFooter = true
            }
        };
    }

    /// <summary>
    /// Exemplo: usar presets pré-configurados
    /// </summary>
    public static ReportRequest CreateWithPresets()
    {
        return new ReportRequest
        {
            Template = RefactoredTemplate,
            FileName = "relatorio.pdf",
            Data = new { name = "Teste" },
            Mode = TemplateMode.Builder,
            PageSettings = PageSettings.WithWatermark("DRAFT")  // Preset com marca d'água
        };
    }

    /// <summary>
    /// Cenários de uso práticos
    /// </summary>
    public static class UseCases
    {
        /// Para um relatório formal
        public static PageSettings FormalReport() => PageSettings.Default();

        /// Para um rascunho que precisa de marca d'água
        public static PageSettings DraftReport() => PageSettings.WithWatermark("DRAFT");

        /// Para etiquetas de envio compactas
        public static PageSettings ShippingLabel() => new()
        {
            PageSize = "A4",
            MarginHorizontal = 0.5f,
            MarginVertical = 0.5f,
            DefaultFontSize = 10,
            ShowHeader = false,
            ShowFooter = false
        };

        /// Para documento confidencial
        public static PageSettings ConfidentialDocument() => new()
        {
            PageSize = "A4",
            MarginHorizontal = 2.0f,
            MarginVertical = 2.0f,
            WatermarkText = "CONFIDENCIAL",
            WatermarkColor = "#FF0000",
            WatermarkOpacity = 0.1f,
            WatermarkFontSize = 80,
            BackgroundColor = "#FFF8DC"  // Cor levemente amarelada
        };

        /// Para layout horizontal (paisagem)
        public static PageSettings Landscape() => PageSettings.Letter();

        /// Para documento compacto
        public static PageSettings Compact() => PageSettings.A4Compact();
    }
}
