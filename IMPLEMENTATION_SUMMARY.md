# 🎨 PageSettings — Configuração de Página Parametrizável

## ✅ O que foi implementado

Refatorei o sistema Buelo para permitir **configurações de página totalmente parametrizáveis** através de uma nova classe `PageSettings`. Agora você pode configurar tamanho da página, margens, cores, marca d'água e muito mais **diretamente no relatório**, sem precisar hardcodear nada no template.

---

## 📋 Resumo das Mudanças

### 1. **Nova Classe: `PageSettings`** (Buelo.Contracts)
```csharp
public class PageSettings
{
    public string PageSize { get; set; } = "A4";                    // A4, Letter, Legal, etc
    public float MarginHorizontal { get; set; } = 2.0f;             // em centímetros
    public float MarginVertical { get; set; } = 2.0f;               // em centímetros
    public string BackgroundColor { get; set; } = "#FFFFFF";        // hex
    public string DefaultTextColor { get; set; } = "#000000";       // hex
    
    // Marca d'água
    public string? WatermarkText { get; set; }
    public string WatermarkColor { get; set; } = "#CCCCCC";
    public float WatermarkOpacity { get; set; } = 0.3f;
    public int WatermarkFontSize { get; set; } = 60;
    
    // Tipografia e Layout
    public int DefaultFontSize { get; set; } = 12;
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; } = true;
}
```

### 2. **`ReportContext` Estendido**
Agora inclui a propriedade `PageSettings`:
```csharp
public class ReportContext
{
    public dynamic Data { get; set; }
    public IHelperRegistry Helpers { get; set; }
    public IDictionary<string, object>? Globals { get; set; }
    public PageSettings PageSettings { get; set; } = PageSettings.Default();  // ← NOVO
}
```

### 3. **`TemplateRecord` Estendido**
Agora permite persistir as configurações com o template:
```csharp
public class TemplateRecord
{
    // ... propriedades existentes ...
    public PageSettings PageSettings { get; set; } = PageSettings.Default();  // ← NOVO
}
```

### 4. **`ReportRequest` e `TemplateRenderRequest` Estendidos**
Permitem passar configurações na requisição:
```csharp
public class ReportRequest
{
    public string Template { get; set; }
    public string FileName { get; set; } = "report.pdf";
    public object Data { get; set; }
    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;
    public PageSettings? PageSettings { get; set; }  // ← NOVO (opcional)
}

public class TemplateRenderRequest
{
    public object? Data { get; set; }
    public string? FileName { get; set; }
    public PageSettings? PageSettings { get; set; }  // ← NOVO (opcional)
}
```

### 5. **`TemplateEngine` Atualizado**
Os métodos agora aceitam `PageSettings`:
```csharp
public async Task<byte[]> RenderAsync(
    string template, 
    object data, 
    TemplateMode mode = TemplateMode.FullClass, 
    PageSettings? pageSettings = null  // ← NOVO
)

public Task<byte[]> RenderTemplateAsync(
    TemplateRecord template, 
    object data, 
    PageSettings? pageSettings = null  // ← NOVO
)
```

### 6. **`ReportController` Atualizado**
Passa `PageSettings` através do pipeline de rendering.

---

## 🚀 Como Usar

### Opção 1: Template Builder com Configurações

```csharp
const string template = @"
Document.Create(container => { 
    container.Page(page => { 
        var settings = ctx.PageSettings;
        page.Size(GetPageSize(settings.PageSize));
        page.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre); 
        page.PageColor(ParseColor(settings.BackgroundColor));
        page.DefaultTextStyle(x => x.FontSize(settings.DefaultFontSize));
        
        page.Header().Text((string)data.name).SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);
        page.Content().Column(x => { 
            x.Item().Text(Placeholders.LoremIpsum()); 
            x.Item().Image(Placeholders.Image(200, 100)); 
        });
        
        if (!string.IsNullOrEmpty(settings.WatermarkText))
        {
            page.Background()
                .AlignCenter().AlignMiddle()
                .Text(settings.WatermarkText)
                .FontSize(settings.WatermarkFontSize)
                .Opacity(settings.WatermarkOpacity);
        }
        
        page.Footer().AlignCenter().Text(x => { 
            x.Span(""Page ""); 
            x.CurrentPageNumber(); 
        }); 
    }); 
}).GeneratePdf()
";

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
        DefaultFontSize = 20,
        WatermarkText = "CONFIDENCIAL"
    }
};
```

### Opção 2: Usar Presets Pré-configurados

```csharp
// Padrão A4 com 2cm margens
var settings = PageSettings.Default();

// Letter com margens de 1"
var settings = PageSettings.Letter();

// A4 compacto com 1cm margens
var settings = PageSettings.A4Compact();

// Com marca d'água
var settings = PageSettings.WithWatermark("DRAFT");
```

### Opção 3: Salvar Template com Configurações

```csharp
var template = new TemplateRecord
{
    Name = "Relatório de Vendas",
    Template = /* seu template aqui */,
    Mode = TemplateMode.Builder,
    PageSettings = new PageSettings
    {
        PageSize = "A4",
        MarginHorizontal = 2.0f,
        MarginVertical = 2.5f,
        WatermarkText = "CONFIDENCIAL"
    }
};

await store.SaveAsync(template);
```

---

## 🔄 Precedência de Configurações

Ao renderizar, as configurações são aplicadas nesta ordem:

1. **PageSettings fornecida na requisição** (maior prioridade)
2. **PageSettings do TemplateRecord** (se template é savedno banco)
3. **PageSettings.Default()** (fallback)

```
Request PageSettings?  → Usar esse
                ↓ não
Template PageSettings? → Usar esse
                ↓ não
PageSettings.Default() → Usar esse (A4, 2cm margens)
```

---

## 📁 Arquivos Criados/Modificados

### Criados:
- `Buelo.Contracts/PageSettings.cs` — Nova classe com todas as configurações
- `PAGE_SETTINGS_GUIDE.md` — Documentação completa
- `Buelo.Tests/Engine/PageSettingsExamples.cs` — Templates de exemplo
- `Buelo.Tests/Engine/PageSettingsEngineTests.cs` — Testes unitários

### Modificados:
- `ReportContext.cs` — Adicionado `PageSettings`
- `TemplateRecord.cs` — Adicionado `PageSettings`
- `ReportRequest.cs` — Adicionado `PageSettings?`
- `TemplateRenderRequest.cs` — Adicionado `PageSettings?`
- `TemplateEngine.cs` — Atualizado para aceitar `PageSettings`
- `ReportController.cs` — Passa `PageSettings` no pipeline
- `ReportControllerTests.cs` — Adicionados testes de `PageSettings`

---

## ✅ Testes

✅ **28 testes passando** — Incluindo:
- Testes de presets (`Default()`, `Letter()`, `A4Compact()`, `WithWatermark()`)
- Testes de rendering com configurações customizadas
- Testes de override de configurações na requisição
- Testes de fallback para `TemplateRecord.PageSettings`

---

## 🎯 Exemplos de Uso Por Cenário

### Relatório Formal
```csharp
PageSettings.Default()
```

### Rascunho com Marca d'água
```csharp
PageSettings.WithWatermark("DRAFT")
```

### Etiqueta Compacta
```csharp
new PageSettings 
{ 
    PageSize = "A4", 
    MarginHorizontal = 0.5f, 
    MarginVertical = 0.5f,
    ShowHeader = false,
    ShowFooter = false
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

---

## 🔄 Compatibilidade

✅ **Totalmente compatível com código existente** — `PageSettings` é optional em todas as requisições. Se não fornecido, usa padrões sensatos.

---

## 📝 Próximos Passos (Opcionais)

1. Armazenar `PageSettings` em banco de dados (se mudar para persistência)
2. UI para editar `PageSettings` visualmente
3. Suporte para diferentes tamanhos de papel customizados
4. Temas/presets salvos
5. Historial de versões de templates com configurações diferentes

---

## 🎉 Resultado Final

Você agora pode:
- ✅ Passar a declaração inteira do Document.Create() como template Builder
- ✅ Configurar tamanho, margens, cor, marca d'água diretamente no relatório
- ✅ Parametrizar todas as configurações de página via `ctx.PageSettings`
- ✅ Reutilizar templates com diferentes configurações
- ✅ Usar presets pré-configurados para cenários comuns
