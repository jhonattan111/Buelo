# Sprint 17 — Backend: Extensible Output Renderers (PDF + Excel Foundation)

## Goal
Decouple the render pipeline from QuestPDF. Introduce an `IOutputRenderer` abstraction so alternative output formats (Excel, HTML, DOCX) can be added as pluggable implementations without touching core engine logic. Implement `PdfRenderer` (wrapping existing QuestPDF path) and `ExcelRenderer` (initial implementation using ClosedXML). The format is selected via a `format` query parameter on render endpoints.

## Status
`[ ] pending`

## Dependencies
- Sprint 14 complete ✅ (`BueloDslDocument` AST available for format-specific rendering)
- Sprint 15 complete ✅ (`BueloProject.DefaultOutputFormat` available)

---

## Design Principles

- Existing render behavior is **unchanged** — `format=pdf` (or no format param) produces the same PDF as before
- Excel rendering targets the `.buelo` DSL component model; Sections-mode (raw C# QuestPDF) is PDF-only by definition
- Format-specific directives (e.g., Excel sheet names) are stored in a `@format` directive block (future) — for now, use project/template defaults
- New NuGet: `ClosedXML` for Excel generation

---

## Backend Scope

### 17.1 — `IOutputRenderer` interface

File: `Buelo.Contracts/IOutputRenderer.cs`

```csharp
public interface IOutputRenderer
{
    /// <summary>Format identifier used in the ?format= query parameter.</summary>
    string Format { get; }

    /// <summary>MIME type for the HTTP response Content-Type header.</summary>
    string ContentType { get; }

    /// <summary>File extension for Content-Disposition attachment filename.</summary>
    string FileExtension { get; }

    /// <summary>Returns true if this renderer supports the given template mode.</summary>
    bool SupportsMode(TemplateMode mode);

    /// <summary>Renders the report and returns raw bytes.</summary>
    Task<byte[]> RenderAsync(RendererInput input, CancellationToken cancellationToken = default);
}
```

---

### 17.2 — `RendererInput` model

File: `Buelo.Contracts/RendererInput.cs`

```csharp
public class RendererInput
{
    /// <summary>Template source code (Sections-mode C# or .buelo DSL).</summary>
    public string Source { get; set; } = string.Empty;
    public TemplateMode Mode { get; set; }
    public ReportContext Context { get; set; } = new();
    
    /// <summary>Parsed .buelo AST — available only when Mode == BueloDsl. Null otherwise.</summary>
    public BueloDslDocument? BueloDslDocument { get; set; }
    
    /// <summary>Resolved page settings after cascade (project → template → request).</summary>
    public PageSettings PageSettings { get; set; } = new();

    /// <summary>Optional format-specific hints (e.g., Excel sheet name).</summary>
    public IDictionary<string, string> FormatHints { get; set; } = new Dictionary<string, string>();
}
```

---

### 17.3 — `PdfRenderer`

File: `Buelo.Engine/Renderers/PdfRenderer.cs`

Implements `IOutputRenderer`:
- `Format = "pdf"`
- `ContentType = "application/pdf"`
- `FileExtension = ".pdf"`
- `SupportsMode`: all modes (`Sections`, `Partial`, `BueloDsl`, legacy)
- `RenderAsync`: delegates to existing `TemplateEngine` Roslyn + QuestPDF pipeline

---

### 17.4 — `ExcelRenderer`

File: `Buelo.Engine/Renderers/ExcelRenderer.cs`

Implements `IOutputRenderer`:
- `Format = "excel"`
- `ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"`
- `FileExtension = ".xlsx"`
- `SupportsMode`: only `BueloDsl` (returns unsupported error for Sections/Partial)
- `RenderAsync`: uses `input.BueloDslDocument` to generate Excel

Excel generation logic using ClosedXML:
1. Create `XLWorkbook`, add worksheet named from `input.FormatHints["sheetName"]` or template name or `"Sheet1"`
2. For each `table` component in the AST:
   - Write column headers to row 1 (bold, background from `headerStyle.backgroundColor`)
   - Iterate `input.Context.Data` (as `IEnumerable<dynamic>`) and write one row per item
   - Apply `format: currency` → `XLCell.SetValue(decimal).Style.NumberFormat.SetFormat("R$ #,##0.00")`
   - Apply `format: date` → `"dd/MM/yyyy"`
3. For `report title` / `report resume` text components: add as merged-cell rows above/below the table
4. `page header` / `page footer`: ignored in Excel (Excel has its own header/footer model — future sprint)
5. Auto-fit column widths: `worksheet.Columns().AdjustToContents()`
6. Return as XLSX byte array: `workbook.SaveAs(stream)` → `stream.ToArray()`

Error handling: if mode is not `BueloDsl`, throw `NotSupportedException("Excel rendering requires .buelo DSL mode")`.

---

### 17.5 — `OutputRendererRegistry`

File: `Buelo.Engine/Renderers/OutputRendererRegistry.cs`

```csharp
public class OutputRendererRegistry
{
    public OutputRendererRegistry(IEnumerable<IOutputRenderer> renderers);
    public IOutputRenderer GetRenderer(string format);           // throws if not found
    public IOutputRenderer? TryGetRenderer(string format);      // null if not found
    public IReadOnlyList<string> SupportedFormats { get; }
}
```

---

### 17.6 — Update render endpoints

File: `Buelo.Api/Controllers/ReportController.cs`

**Existing** `POST /api/report/render`:
- Add optional `?format=pdf` query parameter (defaults to `"pdf"`)
- Resolve renderer from `OutputRendererRegistry`
- Set response `Content-Type` and `Content-Disposition: attachment; filename="report{ext}"` from renderer

**Existing** `POST /api/report/render/{id}`:
- Add same `?format=pdf` parameter
- Renderer chosen from `?format`, falling back to `BueloProject.DefaultOutputFormat`

New endpoint:
```
GET /api/report/formats
```
Returns list of supported formats:
```json
[
  { "format": "pdf",   "contentType": "application/pdf",   "fileExtension": ".pdf" },
  { "format": "excel", "contentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "fileExtension": ".xlsx" }
]
```

---

### 17.7 — Excel format hints via `@format` directive (stub)

File: `Buelo.Engine/BueloDsl/BueloDslParser.cs`

Parse (but do not act on) a new `@format` directive:

```yaml
@format
  excel:
    sheetName: Colaboradores
    freezeHeader: true
```

Parsed values are stored in `RendererInput.FormatHints` keyed as `"excel.sheetName"`, `"excel.freezeHeader"` etc. `ExcelRenderer` reads these hints.  
Other formats can add their own hint namespaces in future sprints.

---

### 17.8 — DI registration

File: `Buelo.Engine/EngineExtensions.cs`

```csharp
services.AddSingleton<IOutputRenderer, PdfRenderer>();
services.AddSingleton<IOutputRenderer, ExcelRenderer>();
services.AddSingleton<OutputRendererRegistry>();

// Add ClosedXML via NuGet: <PackageReference Include="ClosedXML" Version="0.102.*" />
```

---

## Tests

File: `Buelo.Tests/Engine/PdfRendererTests.cs`
- `RenderAsync_SectionsMode_ReturnsPdfBytes`
- `RenderAsync_BueloDslMode_ReturnsPdfBytes`

File: `Buelo.Tests/Engine/ExcelRendererTests.cs`
- `RenderAsync_BueloDsl_WithTable_ReturnsValidXlsx`
- `RenderAsync_ColumnHeaders_MatchColumnLabels`
- `RenderAsync_CurrencyFormat_AppliesNumberFormat`
- `RenderAsync_SectionsMode_ThrowsNotSupported`

File: `Buelo.Tests/Engine/OutputRendererRegistryTests.cs`
- `GetRenderer_Pdf_ReturnsPdfRenderer`
- `GetRenderer_Excel_ReturnsExcelRenderer`
- `GetRenderer_Unknown_Throws`

File: `Buelo.Tests/Api/ReportControllerTests.cs` (additions)
- `PostRender_FormatPdf_ReturnsApplicationPdf`
- `PostRender_FormatExcel_ReturnsXlsxContentType`
- `GetFormats_ReturnsAllRegisteredFormats`
