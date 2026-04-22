# 🚀 Buelo - New Architecture (April 2026)

> After analysis and refactoring, we're moving away from custom BueloDsl to leverage QuestPDF's native C# power.

## 📊 Architecture Change

### ❌ Old Approach (Removed)
```
YAML-like .buelo DSL
  ↓ BueloDslParser
  ↓ BueloDslCompiler
  ↓ BueloDslEngine
  ↓ QuestPDF Renderer
  ↓ PDF Output
```

### ✅ New Approach (Active)
```
C# Template (implements IDocument)
  ↓ Validation (syntax check)
  ↓ Instantiation + Data Binding
  ↓ QuestPDF Renderer
  ↓ PDF/Excel Output
```

## 🎯 Core Concepts

### 1. **Templates are C# Classes**
Templates are complete, well-formed C# classes implementing `QuestPDF.Infrastructure.IDocument`:

```csharp
public class InvoiceDocument : IDocument
{
    private readonly dynamic _data;

    public InvoiceDocument(dynamic data) => _data = data;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Invoice #{_data.InvoiceNumber}"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page => {
            page.Size(PageSizes.A4);
            page.Margin(40);
            
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Text("INVOICE").FontSize(24).Bold();
    }

    // ... more composition methods
}
```

### 2. **PageSettings for Configuration**
Instead of hardcoding page properties, use `PageSettings`:

```csharp
public class PageSettings
{
    public string PageSize { get; set; } = "A4";         // A4, Letter, etc
    public float MarginHorizontal { get; set; } = 2.0f;  // cm
    public float MarginVertical { get; set; } = 2.0f;    // cm
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string DefaultTextColor { get; set; } = "#000000";
    public string? WatermarkText { get; set; }
    public float WatermarkOpacity { get; set; } = 0.3f;
    public int DefaultFontSize { get; set; } = 12;
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; } = true;
    // ... more properties
}
```

**Applied at render time:**
```csharp
var request = new ReportRequest {
    Template = "... C# code ...",
    Data = invoiceData,
    PageSettings = new PageSettings {
        PageSize = "Letter",
        MarginHorizontal = 1.5f,
        BackgroundColor = "#F5F5F5"
    }
};

var pdfBytes = await templateEngine.RenderAsync(
    request.Template,
    request.Data,
    pageSettings: request.PageSettings
);
```

### 3. **Global Artefacts as Data Sources**
Store JSON data centrally for reuse:

```json
// Global Artefact: products.json
[
  { "id": 1, "name": "Widget A", "price": 29.99 },
  { "id": 2, "name": "Widget B", "price": 39.99 }
]
```

Template can bind to this:
```csharp
public class ProductCatalogDocument : IDocument
{
    private readonly dynamic _data;  // Gets products.json

    public void Compose(IDocumentContainer container)
    {
        container.Table(table => {
            foreach (var product in _data.Products)
            {
                // Render product row
            }
        });
    }
}
```

## 🏗️ Backend Structure

```
Buelo.Contracts/
├── PageSettings.cs
├── ReportRequest.cs
├── TemplateRecord.cs
├── TemplateMode.cs (FullClass only)
└── ... other contracts

Buelo.Engine/
├── TemplateEngine.cs (core rendering)
├── DefaultHelperRegistry.cs (formatting helpers)
├── Renderers/
│   ├── PdfRenderer.cs (QuestPDF)
│   └── ExcelRenderer.cs (ClosedXML)
├── Validators/
│   ├── CsharpFileValidator.cs
│   └── JsonFileValidator.cs
└── Storage/
    ├── FileSystemTemplateStore.cs
    ├── FileSystemGlobalArtefactStore.cs
    └── ... other stores

Buelo.Api/
├── Controllers/
│   ├── ReportController.cs (render, validate)
│   ├── TemplatesController.cs (CRUD)
│   └── GlobalArtefactsController.cs (data sources)
├── Program.cs
└── ... configuration
```

## 🎨 Frontend Structure

```
BueloWeb/src/
├── pages/ReportEditor/
│   ├── EditorPanel.vue (Monaco C#)
│   ├── PreviewPanel.vue (PDF viewer)
│   └── SettingsPanel.vue
├── components/
│   ├── TemplateGallery.vue
│   └── DataSourceSelector.vue
├── stores/
│   ├── templateStore.ts
│   └── settingsStore.ts
└── services/
    ├── reportService.ts
    └── templateService.ts
```

## 🔄 Complete Rendering Flow

```
1. CREATION
   User writes C# template class in Monaco Editor
   
2. CONFIGURATION
   User configures via Report Settings Panel:
   - Page size, margins, colors
   - Selects data source (Global Artefact)
   - Sets MockData for preview

3. VALIDATION
   Click "Validate"
   → POST /api/report/validate
   → Check syntax, IDocument presence
   → Show errors if any

4. PREVIEW
   Click "Preview"
   → POST /api/report/render
   → Compile template dynamically
   → Bind data
   → Render to PDF
   → Display in viewer

5. EXPORT
   Click "Export as PDF/Excel"
   → Apply final PageSettings
   → Render to bytes
   → Download file

6. STORAGE
   Click "Save Template"
   → Store template + metadata
   → Persist PageSettings
   → Save MockData
   → Create version entry
```

## 🗑️ Removed Components

- ❌ BueloDsl folder (Buelo.Engine/BueloDsl/)
- ❌ BueloDslParser, BueloDslCompiler, BueloDslEngine
- ❌ BueloDslValidator
- ❌ buelo-language folder (BueloWeb)
- ❌ Sprint documents related to DSL

## ✅ Kept Components

- ✅ PageSettings (enhanced)
- ✅ Global Artefacts system
- ✅ File validation (C#, JSON only)
- ✅ QuestPDF rendering
- ✅ Template storage
- ✅ Helper registry

## 📋 Sprint Structure

### Backend Sprints
1. **Sprint B1**: Core Rendering Engine (QuestPDF foundation)
2. **Sprint B2**: Report API & Mock Data Flow
3. **Sprint B3**: Global Artefacts & Data Sources
4. **Sprint B4**: Multi-Format Output (PDF, Excel)

### Frontend Sprints
1. **Sprint F1**: Report Editor & Template Management
2. **Sprint F2**: Report Settings Panel
3. **Sprint F3**: Template Gallery & Organization
4. **Sprint F4**: Workspace Integration & Export

## 🚀 Next Steps

1. ✅ Remove BueloDsl completely
2. ✅ Implement new sprints
3. ⏳ Build Sprint B1 (TemplateEngine refactor)
4. ⏳ Build Sprint F1 (Editor UI)
5. ⏳ Implement full rendering pipeline
6. ⏳ Test with real-world templates
7. ⏳ Deploy to production

## 📚 QuestPDF Resources

- **Official Docs**: https://www.questpdf.com/
- **Example Templates**: See attached reference documents
  - InvoiceDocument.cs
  - FinancialDashboardDocument.cs
  - ProductCatalogDocument.cs
  - SalesPerformanceDocument.cs
  - OperationsSnapshotDocument.cs
- **Community**: Discord, GitHub Issues

## 🎯 Key Advantages of This Approach

1. **No Custom DSL**: Leverage C# directly + full IntelliSense
2. **Type Safety**: Full compile-time checking
3. **Flexibility**: Access all QuestPDF features
4. **Simplicity**: Fewer custom components to maintain
5. **Performance**: Native C# execution, no interpretation
6. **Learning Curve**: Developers know C#, not custom DSL
7. **Debugging**: Standard C# debugging tools work

---

**Last Updated**: April 21, 2026
**Status**: In Progress (Sprint B1)
**Team**: Buelo Team
