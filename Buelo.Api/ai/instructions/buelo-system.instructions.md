---
description: "Use when creating, editing, or extending any part of the Buelo system: templates, endpoints, engine logic, contracts, or persistence. Covers architecture, API surface, template modes, data flow, and conventions."
applyTo: "**/*.cs"
---

# Buelo — System Overview

Buelo is an **ASP.NET Core 10 API** that accepts **C# template code** at runtime, compiles it with **Roslyn**, and returns a **PDF** rendered by [QuestPDF](https://www.questpdf.com/).

---

## Project Structure

```
Buelo.Contracts   – shared interfaces, models, and enums (no business logic)
Buelo.Engine      – Roslyn compiler, PDF generation, in-memory template store
Buelo.Api         – ASP.NET Core controllers and startup
```

**Dependency direction:** `Buelo.Api` → `Buelo.Engine` → `Buelo.Contracts`

Key NuGet packages live in `Buelo.Contracts.csproj`:

- `QuestPDF` — PDF layout engine
- `Microsoft.CodeAnalysis.CSharp.Scripting` — Roslyn scripting for runtime C# compilation

---

## Core Interfaces (Buelo.Contracts)

### `IReport`

```csharp
public interface IReport
{
    byte[] GenerateReport(ReportContext context);
}
```

Every template must implement this interface. The method receives a `ReportContext` and returns raw PDF bytes.

### `ITemplateStore`

Persistence abstraction for `TemplateRecord` objects.

| Method                      | Description                                     |
| --------------------------- | ----------------------------------------------- |
| `GetAsync(Guid id)`         | Returns template or `null`                      |
| `ListAsync()`               | Returns all templates                           |
| `SaveAsync(TemplateRecord)` | Creates (if `Id == Guid.Empty`) or updates      |
| `DeleteAsync(Guid id)`      | Returns `true` if deleted, `false` if not found |

Default implementation: `InMemoryTemplateStore` (data lost on restart). Replace with a DB-backed implementation for persistence.

### `IHelperRegistry`

Formatting helpers available inside templates as `ctx.Helpers` or `helpers`.

| Method                          | Default behavior              |
| ------------------------------- | ----------------------------- |
| `FormatCurrency(decimal value)` | `value.ToString("C")`         |
| `FormatDate(DateTime date)`     | `date.ToString("dd/MM/yyyy")` |

Override by registering a custom `IHelperRegistry` **before** calling `AddBueloEngine()`.

---

## Data Models (Buelo.Contracts)

### `ReportContext`

Passed to every `IReport.GenerateReport` call.

```csharp
public class ReportContext
{
    public dynamic Data { get; set; }           // JSON payload converted to ExpandoObject
    public IHelperRegistry Helpers { get; set; }
    public IDictionary<string, object>? Globals { get; set; }
}
```

### `TemplateRecord`

Persisted template entity.

| Property          | Type             | Description                                   |
| ----------------- | ---------------- | --------------------------------------------- |
| `Id`              | `Guid`           | Auto-assigned on first save                   |
| `Name`            | `string`         | Human-readable name                           |
| `Description`     | `string?`        | Optional description                          |
| `Template`        | `string`         | C# source code                                |
| `Mode`            | `TemplateMode`   | How the source is interpreted                 |
| `DataSchema`      | `string?`        | Optional JSON Schema for data validation/docs |
| `MockData`        | `object?`        | Data used for preview/testing                 |
| `DefaultFileName` | `string`         | Default PDF file name (`report.pdf`)          |
| `CreatedAt`       | `DateTimeOffset` | UTC creation timestamp                        |
| `UpdatedAt`       | `DateTimeOffset` | UTC last-update timestamp                     |

### `ReportRequest`

Request body for inline rendering (`POST /api/report/render`).

| Property   | Type           | Required | Default      |
| ---------- | -------------- | -------- | ------------ |
| `Template` | `string`       | ✅       | —            |
| `FileName` | `string`       | ❌       | `report.pdf` |
| `Data`     | `object`       | ✅       | —            |
| `Mode`     | `TemplateMode` | ❌       | `FullClass`  |

### `TemplateRenderRequest`

Optional body for `POST /api/report/render/{id}`. All fields fall back to values on the stored `TemplateRecord` if omitted.

| Property   | Type      | Fallback                         |
| ---------- | --------- | -------------------------------- |
| `Data`     | `object?` | `TemplateRecord.MockData`        |
| `FileName` | `string?` | `TemplateRecord.DefaultFileName` |

---

## Template Modes (`TemplateMode` enum)

### `FullClass` (default)

The template is a complete C# class implementing `IReport`.

```csharp
public class Report : IReport
{
    public byte[] GenerateReport(ReportContext ctx)
    {
        var data = ctx.Data;
        return Document.Create(c =>
            c.Page(p => p.Content().Text((string)data.name))
        ).GeneratePdf();
    }
}
```

### `Builder`

The template is **only the return expression** of `GenerateReport`. The engine wraps it automatically.

Available variables: `ctx`, `data` (= `ctx.Data`), `helpers` (= `ctx.Helpers`).

```csharp
Document.Create(c =>
    c.Page(p => p.Content().Text((string)data.name))
).GeneratePdf()
```

### `Sections`

The template is declared as top-level semantic blocks. The engine assembles the
`Document.Create(...).GeneratePdf()` scaffolding automatically.

Supported blocks:

- Page config block: `page => { ... }` (optional)
- Header block: `page.Header()...;` (optional)
- Content block: `page.Content()...;` (required)
- Footer block: `page.Footer()...;` (optional)

Imports are supported at top level:

```csharp
@import header from "company-header"
@import footer from "3fa85f64-5717-4562-b3fc-2c963f66afa6"

page => {
    page.Size(PageSizes.A4);
    page.Margin(2, Unit.Centimetre);
}

page.Content().Text((string)data.name);
```

Available variables in sections: `ctx`, `data` (`ctx.Data`), `helpers` (`ctx.Helpers`).

### `Partial`

Reusable fragment meant for import by `Sections` templates. A Partial template
contains only the fluent chain body that follows `page.Header()`,
`page.Content()`, or `page.Footer()`.

Example:

```csharp
.Text("Acme Corp")
.Bold()
.FontSize(18);
```

---

## Engine: `TemplateEngine` (Buelo.Engine)

### `RenderAsync(string template, object data, TemplateMode mode)`

1. Resolves effective mode (`FullClass`, `Builder`, `Sections`, `Partial`) via `ResolveTemplateMode`.
2. If `Builder`, wraps expression via `WrapBuilderTemplate`.
3. If `Sections`, parses source with `SectionsTemplateParser`, resolves `@import` targets from `ITemplateStore`, and wraps via `WrapSectionsTemplateAsync`.
4. If `FullClass`, uses source as-is.
5. Computes a **SHA-256 hash** of the final code string.
6. Checks the in-memory `ConcurrentDictionary<string, IReport>` cache — compiles only on cache miss.
7. Compiles with `CSharpScript.EvaluateAsync<IReport>` (Roslyn).
8. Converts `data` to a dynamic `ExpandoObject` via `System.Text.Json`.
9. Builds a `ReportContext` and calls `IReport.GenerateReport(context)`.
10. Returns raw `byte[]` (PDF).

### `RenderTemplateAsync(TemplateRecord template, object data)`

Thin wrapper over `RenderAsync` that reads `template.Template` and `template.Mode`.

### Roslyn Script Options

Scripts are compiled with access to:

- `QuestPDF.Fluent`, `QuestPDF.Helpers`, `QuestPDF.Infrastructure`
- `Buelo.Contracts`
- `System`
- `Microsoft.CSharp.RuntimeBinder` (for `dynamic`)

---

## API Endpoints

### Report rendering (`ReportController`)

| Method | Route                           | Description                                   |
| ------ | ------------------------------- | --------------------------------------------- |
| `POST` | `/api/report/render`            | Render from inline template                   |
| `POST` | `/api/report/render/{id:guid}`  | Render saved template by GUID; body optional  |
| `POST` | `/api/report/preview/{id:guid}` | Render saved template using stored `MockData` |

All endpoints return `application/pdf` binary response.

**Error responses:**

- `404` — template GUID not found
- `400` — no data available (no body + no `MockData`)

### Template management (`TemplatesController`)

| Method   | Route                      | Description                             |
| -------- | -------------------------- | --------------------------------------- |
| `GET`    | `/api/templates`           | List all templates (metadata only)      |
| `GET`    | `/api/templates/{id:guid}` | Get single template                     |
| `POST`   | `/api/templates`           | Create template (GUID auto-assigned)    |
| `PUT`    | `/api/templates/{id:guid}` | Replace template; preserves `CreatedAt` |
| `DELETE` | `/api/templates/{id:guid}` | Delete template                         |

---

## Service Registration (`EngineExtensions`)

```csharp
builder.Services.AddBueloEngine();
```

Registers as singletons:

- `TemplateEngine`
- `ITemplateStore` → `InMemoryTemplateStore` (swap for persistence)
- `IHelperRegistry` → `DefaultHelperRegistry` (override before calling `AddBueloEngine()`)

Custom registry example:

```csharp
builder.Services.AddSingleton<IHelperRegistry, MyHelperRegistry>(); // register BEFORE
builder.Services.AddBueloEngine();
```

---

## Conventions

- Controllers use **primary constructor injection** (C# 12).
- `ITemplateStore` operations are all **async** (`Task<T>`), even the in-memory implementation.
- Template `Id = Guid.Empty` signals "new record" — `InMemoryTemplateStore.SaveAsync` auto-assigns a GUID.
- `UpdatedAt` is always refreshed on every `SaveAsync`; `CreatedAt` is preserved on updates.
- `QuestPDF.Settings.License = LicenseType.Community` is set in `Program.cs`.
- OpenAPI/Swagger is enabled only in Development via `app.MapOpenApi()`.
