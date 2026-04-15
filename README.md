# Buelo — Dynamic PDF Report Generation API

Buelo is an ASP.NET Core API that compiles **C# template code at runtime** using Roslyn and returns a **PDF** rendered by [QuestPDF](https://www.questpdf.com/). Templates are authored in a declarative DSL (Sections mode), saved and versioned, enriched with artefacts, and rendered on demand.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Architecture Overview](#architecture-overview)
3. [Template Modes](#template-modes)
4. [Template DSL Reference](#template-dsl-reference)
5. [Page Settings](#page-settings)
6. [Helper Registry](#helper-registry)
7. [Template Store](#template-store)
8. [API Reference](#api-reference)
   - [Report Rendering](#report-rendering)
   - [Templates CRUD](#templates-crud)
   - [Artefacts](#artefacts)
   - [Version History](#version-history)
   - [Export / Import](#export--import)
9. [Running Tests](#running-tests)
10. [CI/CD](#cicd)
11. [Technology Recommendation for Persistence](#technology-recommendation-for-persistence)
12. [Step-by-Step: Migrating to PostgreSQL](#step-by-step-migrating-to-postgresql)

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run locally

```bash
git clone https://github.com/jhonattan111/Buelo.git
cd Buelo
dotnet run --project Buelo.Api
```

The API starts on `https://localhost:5238` (or the port shown in the console).

### Quick smoke test

```http
POST https://localhost:5238/api/report/render
Content-Type: application/json

{
  "template": "page.Content().Text((string)data.name);",
  "data": { "name": "Hello, Buelo!" }
}
```

Save the binary response as a `.pdf` file to view it.

---

## Architecture Overview

```
Buelo.Contracts   â€“ interfaces and models (IReport, IHelperRegistry, ITemplateStore, TemplateRecord, PageSettings, â€¦)
Buelo.Engine      â€“ Roslyn-based compiler, DSL parsers, InMemoryTemplateStore, FileSystemTemplateStore
Buelo.Api         â€“ ASP.NET Core controllers, DI wiring, QuestPDF license bootstrap
Buelo.Tests       â€“ xUnit test suite covering engine, store, and API layers
```

### Rendering pipeline

```
POST /api/report/render
  â†’ ReportController
    â†’ TemplateEngine.RenderAsync()
      1. Parse header directives (@data, @settings, @schema, @helper, @import)
      2. Resolve @import partials from store
      3. Build BueloGeneratedHelpers preamble from @helper directives / artefacts
      4. Wrap sections into a full IReport class
      5. Compile with Roslyn CSharpScript (cached by SHA-256 of generated code)
      6. Execute GenerateReport(context) â†’ byte[] PDF via QuestPDF
```

---

## Template Modes

| Mode | Enum value | Description |
|------|-----------|-------------|
| **Sections** | `0` (default) | Declarative DSL: up to four named blocks (page config, header, content, footer). The engine assembles `Document.Create(...)` automatically. |
| **Partial** | `1` | Reusable fluent fragment. Not rendered directly; imported by Sections templates via `@import`. |

> **Auto-detection:** if you omit `mode`, the engine inspects the source. Templates starting with `page =>`, `page.Header(`, `page.Content(`, `page.Footer(`, or `@import` are treated as **Sections**. Everything else that is explicitly set to `Partial` is treated as **Partial**.

---

## Template DSL Reference

All directives must appear at the **top of the template** before any non-directive, non-blank line. They are stripped from the source before compilation.

### `@import` — import a shared partial

```
@import header|footer|content from "name-or-guid"
```

- Resolved by GUID first, then by template name (case-insensitive, mode must be `Partial`).
- Falls back to the inline block for that slot if the target is not found.
- If both an `@import` and an inline block exist for the same slot, the import takes precedence.

### `@settings` — page configuration

```
@settings { size: "A4"; margin: "2cm"; orientation: "Portrait"; }
```

Accepted values: `size` — `A4`, `Letter`, `Legal`, `A3`, `A5`; `margin` — e.g. `"2cm"`, `"1in"`, `"20mm"` (applied to both axes); `orientation` — `Portrait` / `Landscape`.

### `@data` — artefact data reference

```
@data from "artefact-name"
```

Resolves data from the template's artefacts, then falls back to a cross-template GUID reference, then to the request data, then to MockData.

### `@schema` — inline record schema

```
@schema record Invoice(string Customer, decimal Amount);
```

Declares the expected data shape for tooling/introspection. Stripped before compilation.

### `@helper` — inline helper methods

```
@helper FormatTitle(string s) => s.ToUpperInvariant();
```

Generates a `BueloGeneratedHelpers` static class accessible from the template body as `BueloGeneratedHelpers.FormatTitle(...)`.

```
@helper from "artefact-name"
```

Loads helpers from a `.helpers.cs` artefact stored with the template. Takes precedence over inline `@helper` directives.

### Sections syntax

Inside each section the variables `ctx` (`ReportContext`), `data` (`dynamic`), and `helpers` (`IHelperRegistry`) are in scope.

```csharp
// Optional — page configuration block
page => {
    page.Size(PageSizes.A4);
    page.Margin(2, Unit.Centimetre);
    page.PageColor(Colors.White);
    page.DefaultTextStyle(x => x.FontSize(12));
}

// Optional — header slot
page.Header().Text((string)data.title).Bold().FontSize(18);

// Required — content slot
page.Content()
    .PaddingVertical(1, Unit.Centimetre)
    .Column(col => {
        col.Spacing(10);
        col.Item().Text((string)data.body);
    });

// Optional — footer slot
page.Footer().AlignCenter().Text(x => {
    x.Span("Page ");
    x.CurrentPageNumber();
});
```

---

## Page Settings

`PageSettings` controls the layout applied to every rendered page. It can be set on a `TemplateRecord` (persisted default) or overridden per request.

| Property | Default | Description |
|----------|---------|-------------|
| `pageSize` | `"A4"` | `A4`, `Letter`, `Legal`, `A3`, `A5` |
| `marginHorizontal` | `2.0` | Horizontal margin in centimetres |
| `marginVertical` | `2.0` | Vertical margin in centimetres |
| `backgroundColor` | `"#FFFFFF"` | Page background colour (hex) |
| `defaultTextColor` | `"#000000"` | Default text colour (hex) |
| `defaultFontSize` | `12` | Default font size (pt) |
| `showHeader` | `true` | Whether the header slot is rendered |
| `showFooter` | `true` | Whether the footer slot is rendered |
| `watermarkText` | `null` | Watermark text (null = no watermark) |
| `watermarkColor` | `"#CCCCCC"` | Watermark colour |
| `watermarkOpacity` | `0.3` | Watermark opacity (0â€“1) |
| `watermarkFontSize` | `60` | Watermark font size |

**Static factory methods:**

```csharp
PageSettings.Default()              // A4, 2 cm margins
PageSettings.Letter()               // Letter, ~2.54 cm margins
PageSettings.A4Compact()            // A4, 1 cm margins
PageSettings.WithWatermark("DRAFT") // A4, 2 cm margins + watermark
```

---

## Helper Registry

`IHelperRegistry` provides formatting utilities accessible as `helpers` inside every template.

### Default implementation

```csharp
public class DefaultHelperRegistry : IHelperRegistry
{
    public string FormatCurrency(decimal value) => value.ToString("C");
    public string FormatDate(DateTime date)     => date.ToString("dd/MM/yyyy");
}
```

### Custom implementation

```csharp
// Register before AddBueloEngine() so TryAddSingleton is a no-op
builder.Services.AddSingleton<IHelperRegistry, MyHelperRegistry>();
builder.Services.AddBueloEngine();
```

```csharp
public class MyHelperRegistry : IHelperRegistry
{
    private static readonly CultureInfo BrCulture = new("pt-BR");
    public string FormatCurrency(decimal value) => value.ToString("C", BrCulture);
    public string FormatDate(DateTime date)     => date.ToString("dd 'de' MMMM 'de' yyyy", BrCulture);
}
```

---

## Template Store

### In-memory (default)

Registered automatically by `AddBueloEngine()`. Thread-safe, keeps up to **20 version snapshots** per template (FIFO). Data is cleared on process restart.

```csharp
builder.Services.AddBueloEngine();
```

### File-system

Persists templates to disk. Each template gets its own directory with separate files for metadata, source, artefacts, and snapshots.

```
templates/
  {template-id}/
    template.record.json     # metadata (id, name, mode, dataSchema, â€¦)
    template.report.cs       # template source code
    {artefactName}{.ext}     # one file per artefact
    versions/
      1.snapshot.json
      2.snapshot.json
```

```csharp
// Uses IConfiguration["Buelo:TemplateStorePath"] or falls back to "templates/"
builder.Services.AddBueloFileSystemStore();

// Or with an explicit path:
builder.Services.AddBueloFileSystemStore("/data/templates");
```

Add to `appsettings.json` to configure the path via config:

```json
{
  "Buelo": {
    "TemplateStorePath": "/data/templates"
  }
}
```

---

## API Reference

### Report Rendering

#### `POST /api/report/render`

Render a template inline (not persisted).

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `template` | `string` | âœ… | Template source in Sections DSL |
| `data` | `object` | âœ… | Arbitrary JSON, available as `data` (dynamic) |
| `fileName` | `string` | âŒ | Output file name. Default: `report.pdf` |
| `mode` | `"Sections"` \| `"Partial"` | âŒ | Template mode. Auto-detected when omitted. |
| `pageSettings` | `PageSettings` | âŒ | Page layout overrides |

Returns `application/pdf`.

#### `POST /api/report/validate`

Compile a template without rendering. Returns all Roslyn diagnostics.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `template` | `string` | âœ… | Template source |
| `mode` | `"Sections"` \| `"Partial"` | âŒ | Template mode |

Response: `{ "valid": true, "errors": [] }` — always `200 OK`.

#### `POST /api/report/render/{id:guid}?version={n}`

Render a saved template by its GUID.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `data` | `object` | âŒ | Render data. Falls back to template `MockData` |
| `fileName` | `string` | âŒ | Falls back to template `DefaultFileName` |
| `pageSettings` | `PageSettings` | âŒ | Falls back to template `PageSettings` |
| `version` (query) | `int` | âŒ | Render a historical version snapshot |

Returns `application/pdf`, `404` if unknown, `400` if no data is available.

#### `POST /api/report/preview/{id:guid}`

Render using the template's built-in `MockData`. Returns `400` if `MockData` is null.

---

### Templates CRUD

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/templates` | List all saved templates |
| `GET` | `/api/templates/{id}` | Get one template by GUID |
| `POST` | `/api/templates` | Create a template (GUID auto-assigned, `CreatedAt` set) |
| `PUT` | `/api/templates/{id}` | Replace an existing template |
| `DELETE` | `/api/templates/{id}` | Delete a template. Returns `204` or `404` |

**`TemplateRecord` fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | `Guid` | Auto-assigned on create |
| `name` | `string` | Human-readable name |
| `description` | `string?` | Optional description |
| `template` | `string` | Template source code |
| `mode` | `TemplateMode` | `Sections` or `Partial` |
| `dataSchema` | `string?` | JSON Schema string describing the expected data shape |
| `mockData` | `object?` | Sample data for preview and fallback |
| `defaultFileName` | `string` | Default output file name |
| `pageSettings` | `PageSettings` | Default page settings for this template |
| `artefacts` | `TemplateArtefact[]` | Named file attachments |
| `createdAt` | `DateTimeOffset` | Auto-set on first save |
| `updatedAt` | `DateTimeOffset` | Updated on every save |

---

### Artefacts

Artefacts are named file attachments stored with a template (e.g. data files `.json`, helper code `.helpers.cs`).

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/templates/{id}/artefacts` | List artefact names and extensions |
| `GET` | `/api/templates/{id}/artefacts/{name}` | Get artefact with content |
| `PUT` | `/api/templates/{id}/artefacts/{name}` | Create or replace an artefact |
| `DELETE` | `/api/templates/{id}/artefacts/{name}` | Delete an artefact. Returns `204` or `404` |

`PUT` request body: `{ "extension": ".json", "content": "..." }`

---

### Version History

Every `PUT /api/templates/{id}` auto-snapshots the current state before overwriting.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/templates/{id}/versions` | List all version summaries `{ version, savedAt, savedBy }` |
| `GET` | `/api/templates/{id}/versions/{n}` | Get a full version snapshot (includes template source + artefacts) |
| `POST` | `/api/templates/{id}/versions/{n}/restore` | Restore version `n`: saves current state as new version, then overwrites with snapshot |

---

### Export / Import

#### `GET /api/templates/{id}/export`

Downloads a ZIP archive: `{name}-{id}.zip`

```
template.record.json   # metadata (id, name, mode, dataSchema, mockData, â€¦)
template.report.cs     # template source
{artefactName}{.ext}   # one entry per artefact
```

#### `POST /api/templates/import`

Upload a ZIP file (`multipart/form-data`). A new GUID is assigned. Returns `201 Created` with the new `TemplateRecord`.

---

## Running Tests

```bash
dotnet test Buelo.slnx
```

With coverage:

```bash
dotnet test Buelo.slnx --collect:"XPlat Code Coverage"
```

**Test coverage areas:**

| Area | File |
|------|------|
| Template engine (Sections rendering, auto-detection) | `Engine/TemplateEngineTests.cs` |
| Sections DSL parser | `Engine/SectionsTemplateParserTests.cs` |
| Header directive parser (`@data`, `@settings`, `@helper`, â€¦) | `Engine/TemplateHeaderParserTests.cs` |
| PageSettings factories and rendering | `Engine/PageSettingsEngineTests.cs` |
| Version history | `Engine/TemplateVersioningTests.cs` |
| InMemoryTemplateStore CRUD + versioning | `Engine/InMemoryTemplateStoreTests.cs` |
| FileSystemTemplateStore CRUD + artefacts + versioning | `Engine/FileSystemTemplateStoreTests.cs` |
| Dynamic @helper generation | `Engine/DynamicHelpersTests.cs` |
| Mode auto-detection heuristics | `Engine/TemplateModeDetectionTests.cs` |
| ReportController (render, preview, validation) | `Api/ReportControllerTests.cs` |
| TemplatesController (CRUD, Partial mode) | `Api/TemplatesControllerTests.cs` |

---

## CI/CD

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [master]
  pull_request:
    branches: [master]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore
        run: dotnet restore Buelo.slnx

      - name: Build
        run: dotnet build Buelo.slnx --configuration Release --no-restore

      - name: Format check
        run: dotnet format Buelo.slnx --verify-no-changes

      - name: Test
        run: dotnet test Buelo.slnx --configuration Release --no-build --collect:"XPlat Code Coverage"

      - name: Upload artifacts
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: |
            **/TestResults/**
            **/coverage.cobertura.xml
```

Local parity (run before opening a PR):

```bash
dotnet restore Buelo.slnx
dotnet build Buelo.slnx -c Release --no-restore
dotnet format Buelo.slnx --verify-no-changes
dotnet test Buelo.slnx -c Release --no-build --collect:"XPlat Code Coverage"
```

---

## Technology Recommendation for Persistence

The default `InMemoryTemplateStore` is suitable for development and testing. The `FileSystemTemplateStore` adds durable disk persistence. For multi-instance deployments, a database-backed store is recommended.

### Recommended: **PostgreSQL + Entity Framework Core**

| Criteria | PostgreSQL | SQLite | SQL Server |
|----------|-----------|--------|------------|
| Open source | âœ… | âœ… | âŒ |
| Production-ready | âœ… | âš ï¸ (single-writer) | âœ… |
| Cloud-managed | âœ… (Supabase, Neon, Railway, Azure, AWS RDS) | âŒ | âœ… |
| Native JSON column | âœ… (`jsonb`) | âš ï¸ | âœ… |
| .NET EF Core | âœ… | âœ… | âœ… |

---

## Step-by-Step: Migrating to PostgreSQL

### 1. Add NuGet packages

```bash
dotnet add Buelo.Engine package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Buelo.Engine package Microsoft.EntityFrameworkCore.Design
```

### 2. Create the EF Core DbContext

```csharp
// Buelo.Engine/Data/BueloDbContext.cs
using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Engine.Data;

public class BueloDbContext(DbContextOptions<BueloDbContext> options) : DbContext(options)
{
    public DbSet<TemplateRecord> Templates => Set<TemplateRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateRecord>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            e.Property(t => t.Template).IsRequired();
            e.Property(t => t.MockData)
             .HasColumnType("jsonb")
             .HasConversion(
                 v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                 v => JsonSerializer.Deserialize<object>(v, (JsonSerializerOptions?)null));
        });
    }
}
```

### 3. Implement `ITemplateStore`

```csharp
// Buelo.Engine/Data/EfTemplateStore.cs
public class EfTemplateStore(BueloDbContext db) : ITemplateStore
{
    public Task<TemplateRecord?> GetAsync(Guid id)          => db.Templates.FindAsync(id).AsTask();
    public async Task<IEnumerable<TemplateRecord>> ListAsync() => await db.Templates.ToListAsync();

    public async Task<TemplateRecord> SaveAsync(TemplateRecord t)
    {
        if (t.Id == Guid.Empty) { t.Id = Guid.NewGuid(); t.CreatedAt = DateTimeOffset.UtcNow; db.Templates.Add(t); }
        else                    { t.UpdatedAt = DateTimeOffset.UtcNow; db.Templates.Update(t); }
        await db.SaveChangesAsync();
        return t;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var t = await db.Templates.FindAsync(id);
        if (t is null) return false;
        db.Templates.Remove(t);
        await db.SaveChangesAsync();
        return true;
    }
}
```

### 4. Register services

```csharp
// Buelo.Api/Program.cs
builder.Services.AddDbContext<BueloDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITemplateStore, EfTemplateStore>(); // register before AddBueloEngine
builder.Services.AddBueloEngine();
```

### 5. Connection string

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=buelo;Username=postgres;Password=yourpassword"
  }
}
```

### 6. Migrations

```bash
dotnet ef migrations add InitialCreate --project Buelo.Engine --startup-project Buelo.Api
dotnet ef database update           --project Buelo.Engine --startup-project Buelo.Api
```

---

## License

MIT — see [LICENSE.txt](LICENSE.txt).
