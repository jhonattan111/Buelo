# PageSettings — Configuração de Página Parametrizável

## Visão Geral

O sistema `PageSettings` permite que você configure dinamicamente todos os aspectos visuais de uma página PDF diretamente no seu relatório, sem precisar hardcodear valores dentro do template.

## O que é Configurável

A classe `PageSettings` oferece controle sobre:

- **Tamanho da página**: A4, Letter, Legal, A3, A5
- **Margens**: Horizontal e vertical (em centímetros)
- **Cores**: Cor de fundo e cor padrão do texto
- **Marca d'água**: Texto, cor, opacidade e tamanho da fonte
- **Headers/Footers**: Ativar ou desativar
- **Fonte padrão**: Tamanho da fonte para corpo do texto

## Arquitetura

### Fluxo de Dados

```
ReportRequest/TemplateRenderRequest
    ↓
    PageSettings (opcional)
    ↓
ReportController
    ↓
TemplateEngine.RenderAsync(template, data, mode, pageSettings)
    ↓
ReportContext
    ├─ ctx.Data
    ├─ ctx.Helpers
    ├─ ctx.Globals
    └─ ctx.PageSettings ← AQUI!
    ↓
IReport.GenerateReport(ctx)
    ↓
PDF com as configurações aplicadas
```

### Precedência

1. **Request PageSettings** (se fornecido) — sobrescreve tudo
2. **TemplateRecord.PageSettings** — padrão para renderização
3. **PageSettings.Default()** — fallback global (A4 com 2cm de margens)

## Exemplos de Uso

### 1. Usar Padrões Pré-configurados

```csharp
// Renderizar com padrões de fábrica
var settings = PageSettings.Letter();           // Letter com margens de 1"
var settings = PageSettings.A4Compact();        // A4 com margens de 1cm
var settings = PageSettings.WithWatermark("DRAFT");  // Com marca d'água
```

### 2. Template Builder com PageSettings

```csharp
// Template Builder mode — acesso às configurações via ctx
const string template = @"
Document.Create(c => 
{
    c.Page(p => 
    {
        var settings = ctx.PageSettings;
        
        p.Size(GetPageSize(settings.PageSize));
        p.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre);
        
        p.Header()
            .Text((string)data.name)
            .FontSize(36)
            .FontColor(Colors.Blue.Medium);
            
        p.Content()
            .Column(x => 
            {
                x.Item().Text(""Conteúdo aqui"");
            });
            
        p.Footer()
            .AlignCenter()
            .Text(x => 
            {
                x.Span(""Página "");
                x.CurrentPageNumber();
            });
    });
}).GeneratePdf()
";

// Enviar via API
var request = new ReportRequest
{
    Template = template,
    FileName = "meu-relatorio.pdf",
    Data = new { name = "Relatório Importante" },
    PageSettings = new PageSettings
    {
        PageSize = "A4",
        MarginHorizontal = 2.0f,
        MarginVertical = 2.0f,
        BackgroundColor = "#F5F5F5",
        WatermarkText = "CONFIDENCIAL",
        WatermarkOpacity = 0.2f
    }
};

var response = await client.PostAsJsonAsync("/api/report/render", request);
```

### 2.1 Sections Mode com fallback para PageSettings

No modo `Sections`, se o bloco `page => { ... }` for omitido, o engine aplica
automaticamente `ctx.PageSettings` (tamanho, margens e fonte padrão). Você só
declara header/body/footer de forma fluente.

```csharp
const string sectionsTemplate = @"
@import header from ""company-header""

page.Content()
    .PaddingVertical(1, Unit.Centimetre)
    .Column(x =>
    {
        x.Spacing(8);
        x.Item().Text((string)data.name);
        x.Item().Text(""Relatório gerado em modo Sections"");
    });

page.Footer()
    .AlignCenter()
    .Text(x => { x.Span(""Página ""); x.CurrentPageNumber(); });
";

var request = new ReportRequest
{
    Template = sectionsTemplate,
    Mode = TemplateMode.Sections,
    Data = new { name = "Relatório Comercial" },
    PageSettings = new PageSettings
    {
        PageSize = "Letter",
        MarginHorizontal = 1.5f,
        MarginVertical = 2.0f,
        DefaultFontSize = 11
    }
};
```

Se quiser sobrescrever visualmente as configurações de página dentro do próprio
template, inclua o bloco `page => { ... }` explicitamente.

### 3. FullClass Template com PageSettings

```csharp
public class Report : IReport
{
    public byte[] GenerateReport(ReportContext ctx)
    {
        var data = ctx.Data;
        var settings = ctx.PageSettings;
        
        return Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(GetPageSize(settings.PageSize));
                p.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre);
                
                // Aplicar marca d'água se configurada
                if (!string.IsNullOrEmpty(settings.WatermarkText))
                {
                    p.Background()
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(settings.WatermarkText)
                        .FontSize(settings.WatermarkFontSize)
                        .Opacity(settings.WatermarkOpacity);
                }
                
                p.Header()
                    .Text((string)data.name)
                    .SemiBold()
                    .FontSize(36);
                    
                p.Content()
                    .Text((string)data.description);
            });
        }).GeneratePdf();
    }
    
    private static PageSize GetPageSize(string size) => size.ToUpper() switch
    {
        "LETTER" => PageSizes.Letter,
        "LEGAL" => PageSizes.Legal,
        "A3" => PageSizes.A3,
        "A4" => PageSizes.A4,
        "A5" => PageSizes.A5,
        _ => PageSizes.A4
    };
}
```

### 4. Salvar Template com Configurações

```csharp
var template = new TemplateRecord
{
    Name = "Relatório de Vendas",
    Description = "Relatório mensal com marca d'água",
    Template = @"Document.Create(...).GeneratePdf()",
    Mode = TemplateMode.Builder,
    MockData = new { /* ... */ },
    DefaultFileName = "vendas.pdf",
    
    // Configurações que serão o padrão
    PageSettings = new PageSettings
    {
        PageSize = "A4",
        MarginHorizontal = 2.0f,
        MarginVertical = 2.5f,
        BackgroundColor = "#FFFFFF",
        WatermarkText = "CÓPIA INTERNA",
        WatermarkColor = "#DEDEDE",
        WatermarkOpacity = 0.15f,
        WatermarkFontSize = 50,
        DefaultFontSize = 11,
        DefaultTextColor = "#333333",
        ShowHeader = true,
        ShowFooter = true
    }
};

await store.SaveAsync(template);
```

### 5. Renderizar com Override de Configurações

```csharp
// GET /api/report/render/{templateId}
// Body (opcional):
var overrides = new TemplateRenderRequest
{
    Data = new { /* dados */ },
    FileName = "especial.pdf",
    
    // Sobrescreve as configurações do template
    PageSettings = new PageSettings
    {
        PageSize = "Letter",
        WatermarkText = "DRAFT - " + DateTime.Now.ToString("yyyy-MM-dd")
    }
};

var response = await client.PostAsJsonAsync($"/api/report/render/{templateId}", overrides);
```

## Propriedades de PageSettings

```csharp
public class PageSettings
{
    // Tamanho da página (ex: "A4", "Letter", "Legal")
    public string PageSize { get; set; } = "A4";

    // Margens em centímetros
    public float MarginHorizontal { get; set; } = 2.0f;
    public float MarginVertical { get; set; } = 2.0f;

    // Cores (formato hex)
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string DefaultTextColor { get; set; } = "#000000";

    // Marca d'água
    public string? WatermarkText { get; set; }
    public string WatermarkColor { get; set; } = "#CCCCCC";
    public float WatermarkOpacity { get; set; } = 0.3f;
    public int WatermarkFontSize { get; set; } = 60;

    // Tipografia
    public int DefaultFontSize { get; set; } = 12;

    // Layout
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; } = true;
}
```

## Métodos Auxiliares (Factory Methods)

```csharp
// Padrão A4 com 2cm margens
var settings = PageSettings.Default();

// Letter com margens de 1 polegada (2.54cm)
var settings = PageSettings.Letter();

// A4 compacto com 1cm margens
var settings = PageSettings.A4Compact();

// Com marca d'água predefinida
var settings = PageSettings.WithWatermark("CONFIDENTIAL");
```

## Fluxo de Precedência

Ao renderizar um template:

1. Se `PageSettings` for fornecido na request → usar esse
2. Senão, se o template tem `PageSettings` configurado → usar esse
3. Senão → usar `PageSettings.Default()`

## Compatibilidade com Versões Anteriores

- Templates existentes continuam funcionando sem mudanças
- `PageSettings` é opcional em todas as requisições
- Se não fornecido, usa padrões sensatos (A4, margens 2cm)

## Boas Práticas

1. **Para relatórios muito customizáveis**: Use templates `Builder` e acesse `ctx.PageSettings`
2. **Para relatórios padrão**: Configure `PageSettings` no `TemplateRecord` e reutilize
3. **Para overrides dinâmicos**: Forneça `PageSettings` na request quando necessário
4. **Para marca d'água**: Use `PageSettings.WithWatermark()` ou configure manualmente
5. **Para múltiplas variações**: Crie múltiplos `TemplateRecord` com diferentes configurações

## Exemplos de Configuração por Cenário

### Relatório Formal
```csharp
PageSettings.Default()
```

### Relatório de Rascunho
```csharp
PageSettings.WithWatermark("DRAFT")
```

### Etiqueta de Envio
```csharp
new PageSettings 
{ 
    PageSize = "A4", 
    MarginHorizontal = 0.5f, 
    MarginVertical = 0.5f 
}
```

### Documento Confidencial
```csharp
new PageSettings
{
    PageSize = "A4",
    WatermarkText = "CONFIDENCIAL",
    WatermarkOpacity = 0.1f,
    BackgroundColor = "#FFF8DC"
}
```
