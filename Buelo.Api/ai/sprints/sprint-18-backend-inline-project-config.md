# Sprint 18 — Backend: Inline @project Config + OutputFormat per Template

## Goal
Remove the standalone `project.bueloproject` file concept. Page settings and defaults are now declared
directly inside each `.buelo` report file via a `@project` directive block (similar to how `@settings`
already works). The output format (pdf/excel) moves from project level into `TemplateRecord`, so it is
configured once at template creation time.

## Status
`[x] done`

## Dependencies
- Sprint 17 complete ✅ (`IOutputRenderer`, `PdfRenderer`, `ExcelRenderer`, `OutputRendererRegistry`)

---

## Motivation

The `project.bueloproject` file is a global singleton that does not map naturally to the per-report
workflow. Each `.buelo` file IS the report; it should own its page settings. The JSReport-style design
lets you open any report file and see its configuration without navigating to a separate project route.

---

## Tasks

### 18-B.1 — `OutputFormat` enum + `TemplateRecord.OutputFormat`

File: `Buelo.Contracts/TemplateRecord.cs`

Add an `OutputFormat` enum:

```csharp
public enum OutputFormat
{
    Pdf,
    Excel
}
```

Add a property to `TemplateRecord`:

```csharp
/// <summary>
/// The output format this template produces.
/// Defaults to <see cref="OutputFormat.Pdf"/>.
/// Set once at template creation time; not overridable per-request.
/// </summary>
public OutputFormat OutputFormat { get; set; } = OutputFormat.Pdf;
```

---

### 18-B.2 — `@project` directive in `BueloDslAst.cs`

File: `Buelo.Engine/BueloDsl/BueloDslAst.cs`

Add a new record for inline project configuration:

```csharp
/// <summary>
/// Parsed content of the <c>@project</c> directive block in a .buelo file.
/// Overrides the template-record page settings for this specific report.
/// </summary>
public record BueloDslProjectConfig(
    string? PageSize,
    string? Orientation,
    double? MarginHorizontal,
    double? MarginVertical,
    string? BackgroundColor,
    string? DefaultTextColor,
    int? DefaultFontSize,
    bool? ShowHeader,
    bool? ShowFooter,
    string? WatermarkText
);
```

Update `BueloDslDirectives` to include the new field:

```csharp
public record BueloDslDirectives(
    IReadOnlyList<BueloDslImport> Imports,
    string? DataRef,
    BueloDslSettings? Settings,
    BueloDslProjectConfig? ProjectConfig = null,  // ← new
    IReadOnlyDictionary<string, string>? FormatHints = null
);
```

---

### 18-B.3 — Parse `@project` block in `BueloDslParser.cs`

File: `Buelo.Engine/BueloDsl/BueloDslParser.cs`

In `ParseCore`, add a branch inside the directive-phase loop that recognises `@project` lines and
reads the following indented key-value pairs:

```
@project
  pageSize: A4
  orientation: Portrait
  marginHorizontal: 2.0
  marginVertical: 2.0
  backgroundColor: "#FFFFFF"
  defaultTextColor: "#000000"
  defaultFontSize: 12
  showHeader: true
  showFooter: true
  watermarkText: "CONFIDENTIAL"
```

Parsing rules:
- Line starts with `@project` (case-insensitive, no trailing colon required).
- Following lines indented by ≥ 2 spaces are key-value pairs (`key: value`).
- Unknown keys are silently ignored.
- All values are optional; absent keys leave the corresponding field `null` (use template-record default).
- Boolean values accept `true`/`false` (case-insensitive).
- Numeric values use `double.TryParse` / `int.TryParse` with `InvariantCulture`.

---

### 18-B.4 — Map `BueloDslProjectConfig` → `PageSettings` in `TemplateEngine`

File: `Buelo.Engine/TemplateEngine.cs`

After parsing the `.buelo` source, apply `@project` overrides on top of the `TemplateRecord.PageSettings`
cascade:

```csharp
// Existing cascade: project-level → template-record → request
// NEW cascade: project-level → template-record → @project-inline → request

if (doc.Directives.ProjectConfig is { } pc)
{
    if (pc.PageSize is not null) effectiveSettings.PageSize = Enum.Parse<PageSize>(pc.PageSize, true);
    if (pc.MarginHorizontal.HasValue) effectiveSettings.MarginHorizontal = pc.MarginHorizontal.Value;
    if (pc.MarginVertical.HasValue)   effectiveSettings.MarginVertical   = pc.MarginVertical.Value;
    if (pc.BackgroundColor is not null) effectiveSettings.BackgroundColor = pc.BackgroundColor;
    if (pc.DefaultTextColor is not null) effectiveSettings.DefaultTextColor = pc.DefaultTextColor;
    if (pc.DefaultFontSize.HasValue)  effectiveSettings.DefaultFontSize  = pc.DefaultFontSize.Value;
    if (pc.ShowHeader.HasValue)       effectiveSettings.ShowHeader       = pc.ShowHeader.Value;
    if (pc.ShowFooter.HasValue)       effectiveSettings.ShowFooter       = pc.ShowFooter.Value;
    if (pc.WatermarkText is not null) effectiveSettings.WatermarkText    = pc.WatermarkText;
}
```

---

### 18-B.5 — Use `TemplateRecord.OutputFormat` in `ReportController`

File: `Buelo.Api/Controllers/ReportController.cs`

When rendering a saved template by ID:
- Look up `TemplateRecord.OutputFormat` and use it as the default `format` value.
- If the request still supplies an explicit `?format=` query param, that takes precedence (for
  backward compatibility during transition).

```csharp
// Derive format: explicit query param > template record default
var format = Request.Query["format"].FirstOrDefault()
    ?? (template.OutputFormat == OutputFormat.Excel ? "excel" : "pdf");
```

---

### 18-B.6 — Remove `IBueloProjectStore` and related classes

Files to delete:
- `Buelo.Contracts/IBueloProjectStore.cs`
- `Buelo.Contracts/BueloProject.cs`
- `Buelo.Engine/FileSystemBueloProjectStore.cs`
- `Buelo.Engine/InMemoryBueloProjectStore.cs`

---

### 18-B.7 — Remove `ProjectController`

File to delete: `Buelo.Api/Controllers/ProjectController.cs`

---

### 18-B.8 — Update `EngineExtensions.cs`

File: `Buelo.Engine/EngineExtensions.cs`

Remove the `AddBueloFileSystemStore` / DI registration for `IBueloProjectStore`.
Remove any reference to `IBueloProjectStore` from the service collection setup.

---

### 18-B.9 — Update `Program.cs`

File: `Buelo.Api/Program.cs`

Remove the `IBueloProjectStore` service registration call.

---

### 18-B.10 — Tests

File: `Buelo.Tests/Engine/BueloDslParserTests.cs` — add tests:
- `@project` block with all fields is parsed into `BueloDslProjectConfig` correctly.
- `@project` block with partial fields leaves missing fields `null`.
- Unknown keys in `@project` are silently ignored.
- `@project` block without any sub-keys is parsed as an empty `BueloDslProjectConfig`.

File: `Buelo.Tests/Engine/PageSettingsEngineTests.cs` — add tests:
- `@project` inline overrides cascade correctly on top of `TemplateRecord.PageSettings`.

---

## DSL Example

```buelo
@project
  pageSize: A4
  marginHorizontal: 2.5
  marginVertical: 2.5
  showHeader: true
  showFooter: false

@data data/funcionarios.json
@settings
  size: A4

report title:
  text: Relatório de Funcionários
    style: { fontSize: 18, bold: true }
```

---

## Removed API Endpoints

| Method | Path | Status |
|--------|------|--------|
| `GET`  | `/api/project` | **removed** |
| `PUT`  | `/api/project` | **removed** |
| `PATCH`| `/api/project/page-settings` | **removed** |
| `PATCH`| `/api/project/mock-data` | **removed** |
| `GET`  | `/api/project/reset` | **removed** |
