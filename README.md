# Buelo — Dynamic Report Generation API

Buelo is an ASP.NET Core API that accepts **C# template code** at runtime, compiles it with Roslyn, and returns a **PDF** rendered by [QuestPDF](https://www.questpdf.com/).

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Architecture Overview](#architecture-overview)
3. [Current API Reference](#current-api-reference)
4. [Improvement Roadmap](#improvement-roadmap)
   - [Feature 1 – Builder Mode (focused templates)](#feature-1--builder-mode-focused-templates)
   - [Feature 2 – Template Persistence (GUIDs)](#feature-2--template-persistence-guids)
   - [Feature 3 – Data Schema & Mock Data](#feature-3--data-schema--mock-data)
   - [Feature 4 – Custom Helper Registries](#feature-4--custom-helper-registries)
5. [Technology Recommendation for Persistence](#technology-recommendation-for-persistence)
6. [Step-by-Step: Migrating to PostgreSQL](#step-by-step-migrating-to-postgresql)
7. [Step-by-Step: Creating a Test Project](#step-by-step-creating-a-test-project)

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
  "template": "public class Report : IReport { public byte[] GenerateReport(ReportContext ctx) { var data = ctx.Data; return Document.Create(c => c.Page(p => p.Content().Text((string)data.name))).GeneratePdf(); } }",
  "fileName": "hello.pdf",
  "data": { "name": "World" }
}
```

Save the binary response as a `.pdf` file to view it.

---

## Architecture Overview

```
Buelo.Contracts   – shared interfaces and models (IReport, IHelperRegistry, TemplateRecord, …)
Buelo.Engine      – Roslyn-based template compiler + in-memory template store
Buelo.Api         – ASP.NET Core endpoints
```

The key flow:

```
POST /api/report/render
  → ReportController
    → TemplateEngine.RenderAsync()
      → Compile template with Roslyn (cached by hash)
      → Build ReportContext { Data, Helpers, Globals }
      → IReport.GenerateReport(context) → byte[] (PDF)
```

---

## Current API Reference

### `POST /api/report/render`

Render a report from a template sent in the request body.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `template` | `string` | ✅ | C# source code for the report (see `TemplateMode` below) |
| `fileName` | `string` | ❌ | Output file name. Default: `report.pdf` |
| `data` | `object` | ✅ | Arbitrary JSON data available as `ctx.Data` (dynamic) |
| `mode` | `"FullClass"` \| `"Builder"` | ❌ | How the template is interpreted. Default: `FullClass` |

---

### `POST /api/report/render/{id}`

Render a previously saved template by its GUID.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `data` | `object` | ❌ | Data for the report. Falls back to `MockData` if omitted |
| `fileName` | `string` | ❌ | Output file name. Falls back to `DefaultFileName` if omitted |

---

### `POST /api/report/preview/{id}`

Render a saved template using its built-in `MockData`. Returns `400` if no mock data is configured.

---

### Templates CRUD — `POST / GET / PUT / DELETE /api/templates`

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/templates` | List all saved templates |
| `GET` | `/api/templates/{id}` | Get one template by GUID |
| `POST` | `/api/templates` | Create a new template (GUID auto-assigned) |
| `PUT` | `/api/templates/{id}` | Replace an existing template |
| `DELETE` | `/api/templates/{id}` | Delete a template |

---

## Improvement Roadmap

### Feature 1 – Builder Mode (focused templates)

**Goal:** allow templates to contain *only the report body expression*, removing the boilerplate class and method declaration. The engine wraps the expression automatically.

#### How it works

Set `"mode": "Builder"` in the request (or set `Mode = TemplateMode.Builder` on a `TemplateRecord`).

Inside the expression you have access to:

| Variable | Type | Value |
|----------|------|-------|
| `ctx` | `ReportContext` | Full context |
| `data` | `dynamic` | Shorthand for `ctx.Data` |
| `helpers` | `IHelperRegistry` | Shorthand for `ctx.Helpers` |

#### Example – ad-hoc render

```http
POST /api/report/render
Content-Type: application/json

{
  "mode": "Builder",
  "fileName": "invoice.pdf",
  "template": "Document.Create(c => c.Page(p => { p.Margin(20); p.Content().Column(col => { col.Item().Text($\"Customer: {data.customer}\"); col.Item().Text($\"Amount: {helpers.FormatCurrency((decimal)data.amount)}\"); }); })).GeneratePdf()",
  "data": { "customer": "Acme Corp", "amount": 4200.00 }
}
```

#### Example – saved template in Builder mode

```http
POST /api/templates
Content-Type: application/json

{
  "name": "Invoice",
  "mode": "Builder",
  "defaultFileName": "invoice.pdf",
  "template": "Document.Create(c => c.Page(p => p.Content().Text((string)data.customer))).GeneratePdf()",
  "mockData": { "customer": "Acme Corp", "amount": 4200.00 }
}
```

When `TemplateMode.Builder` is set the engine internally generates:

```csharp
public class Report : IReport
{
    public byte[] GenerateReport(ReportContext ctx)
    {
        var data    = ctx.Data;
        var helpers = ctx.Helpers;
        return /* your expression */;
    }
}
```

---

### Feature 2 – Template Persistence (GUIDs)

**Goal:** save templates so they can be rendered later by a stable GUID, without resending the source code.

This feature is **already implemented** via the `ITemplateStore` abstraction. The default store is `InMemoryTemplateStore` (data is lost on restart). See [Step-by-Step: Migrating to PostgreSQL](#step-by-step-migrating-to-postgresql) to persist data permanently.

#### Typical workflow

```
1. Save template   → POST /api/templates        → 201 { "id": "3fa85f64-...", … }
2. Render it       → POST /api/report/render/3fa85f64-...  { "data": { … } }
3. Preview it      → POST /api/report/preview/3fa85f64-...
4. Update template → PUT  /api/templates/3fa85f64-...
5. Delete template → DELETE /api/templates/3fa85f64-...
```

---

### Feature 3 – Data Schema & Mock Data

**Goal:** document the expected data shape for a template and provide a built-in mock for testing.

Both are fields on `TemplateRecord`:

| Field | Type | Purpose |
|-------|------|---------|
| `dataSchema` | `string` (JSON Schema) | Describes the data contract expected by the template. Use any JSON Schema validator or IDE plugin to validate payloads before rendering. |
| `mockData` | `object` | A sample data object stored alongside the template. Used by `POST /api/report/preview/{id}` and as a fallback when no data is supplied to `POST /api/report/render/{id}`. |

#### Example – template with schema and mock

```json
{
  "name": "Monthly Invoice",
  "mode": "Builder",
  "defaultFileName": "invoice.pdf",
  "template": "Document.Create(c => c.Page(p => p.Content().Text((string)data.customer))).GeneratePdf()",
  "dataSchema": "{\"type\":\"object\",\"required\":[\"customer\",\"amount\"],\"properties\":{\"customer\":{\"type\":\"string\"},\"amount\":{\"type\":\"number\"}}}",
  "mockData": { "customer": "Test Customer", "amount": 99.99 }
}
```

> **Future enhancement:** add a `POST /api/templates/{id}/validate` endpoint that accepts a data payload and validates it against `dataSchema` using a library such as [JsonSchema.Net](https://github.com/gregsdennis/json-everything).

---

### Feature 4 – Custom Helper Registries

**Goal:** remove the hardcoded `DefaultHelperRegistry` from the engine and make helpers a first-class, extensible system feature.

This feature is **already implemented**. `TemplateEngine` no longer instantiates `DefaultHelperRegistry` directly; instead it receives `IHelperRegistry` via constructor injection (DI).

#### Create your own helper registry

```csharp
// MyHelpers.cs
using Buelo.Contracts;
using System.Globalization;

public class MyHelperRegistry : IHelperRegistry
{
    private static readonly CultureInfo BrCulture = new("pt-BR");

    public string FormatCurrency(decimal value)
        => value.ToString("C", BrCulture); // R$ 1.200,00

    public string FormatDate(DateTime date)
        => date.ToString("dd 'de' MMMM 'de' yyyy", BrCulture); // 01 de janeiro de 2026
}
```

#### Register it before `AddBueloEngine()`

```csharp
// Program.cs
builder.Services.AddSingleton<IHelperRegistry, MyHelperRegistry>(); // ← register first
builder.Services.AddBueloEngine();                                   // ← TryAdd is a no-op
```

`AddBueloEngine()` uses `TryAddSingleton<IHelperRegistry, DefaultHelperRegistry>()`, so your registration takes precedence.

#### Extend the interface

To add more helpers, extend `IHelperRegistry` (or create a new interface):

```csharp
// Buelo.Contracts/IHelperRegistry.cs
public interface IHelperRegistry
{
    string FormatCurrency(decimal value);
    string FormatDate(DateTime date);
    string FormatPhone(string phone);   // new helper
}
```

Then update your implementation and access it in Builder-mode templates via `helpers.FormatPhone(...)`.

---

## Technology Recommendation for Persistence

The current `InMemoryTemplateStore` is suitable for development and testing. For production you need durable storage.

### Recommendation: **PostgreSQL + Entity Framework Core**

| Criteria | PostgreSQL | SQLite | SQL Server |
|----------|-----------|--------|------------|
| Open source | ✅ | ✅ | ❌ |
| Production-ready | ✅ | ⚠️ (single-writer) | ✅ |
| Cloud-managed options | ✅ (Supabase, Neon, Railway, Azure, AWS RDS) | ❌ | ✅ |
| JSON column support | ✅ (native `jsonb`) | ⚠️ (basic) | ✅ |
| .NET EF Core support | ✅ | ✅ | ✅ |
| Cost | Free | Free | Paid (Express is free) |

**PostgreSQL is the recommended choice** because:

- `mockData` and `dataSchema` can be stored as native `jsonb` columns, enabling efficient querying.
- It is open-source, battle-tested, and widely available on every major cloud provider.
- The [Npgsql EF Core provider](https://www.npgsql.org/efcore/) has first-class .NET support.

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
            // Store MockData as a JSONB column for efficient querying
            e.Property(t => t.MockData)
             .HasColumnType("jsonb")
             .HasConversion(
                 v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                 v => System.Text.Json.JsonSerializer.Deserialize<object>(v, (System.Text.Json.JsonSerializerOptions?)null));
        });
    }
}
```

### 3. Implement `ITemplateStore` using EF Core

```csharp
// Buelo.Engine/Data/EfTemplateStore.cs
using Buelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Buelo.Engine.Data;

public class EfTemplateStore(BueloDbContext db) : ITemplateStore
{
    public Task<TemplateRecord?> GetAsync(Guid id)
        => db.Templates.FindAsync(id).AsTask();

    public async Task<IEnumerable<TemplateRecord>> ListAsync()
        => await db.Templates.ToListAsync();

    public async Task<TemplateRecord> SaveAsync(TemplateRecord template)
    {
        if (template.Id == Guid.Empty)
        {
            template.Id = Guid.NewGuid();
            template.CreatedAt = DateTimeOffset.UtcNow;
            db.Templates.Add(template);
        }
        else
        {
            template.UpdatedAt = DateTimeOffset.UtcNow;
            db.Templates.Update(template);
        }

        await db.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var template = await db.Templates.FindAsync(id);
        if (template is null) return false;

        db.Templates.Remove(template);
        await db.SaveChangesAsync();
        return true;
    }
}
```

### 4. Register the services

```csharp
// Buelo.Api/Program.cs
builder.Services.AddDbContext<BueloDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITemplateStore, EfTemplateStore>(); // ← register before AddBueloEngine
builder.Services.AddBueloEngine();
```

### 5. Add connection string

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=buelo;Username=postgres;Password=yourpassword"
  }
}
```

### 6. Create and apply migrations

```bash
dotnet ef migrations add InitialCreate --project Buelo.Engine --startup-project Buelo.Api
dotnet ef database update --project Buelo.Engine --startup-project Buelo.Api
```

---

## Step-by-Step: Creating a Test Project

This section shows how to create a test project and cover the main methods in the current codebase.

### 1. Create the test project and add it to the solution

```bash
dotnet new xunit -n Buelo.Tests
dotnet sln Buelo.slnx add Buelo.Tests/Buelo.Tests.csproj
```

### 2. Add project references

```bash
dotnet add Buelo.Tests reference Buelo.Engine
dotnet add Buelo.Tests reference Buelo.Contracts
dotnet add Buelo.Tests reference Buelo.Api
```

### 3. Add useful testing packages

```bash
dotnet add Buelo.Tests package FluentAssertions
dotnet add Buelo.Tests package Moq
dotnet add Buelo.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add Buelo.Tests package coverlet.collector
```

### 4. Suggested folder structure

```text
Buelo.Tests/
    Engine/
        TemplateEngineTests.cs
        InMemoryTemplateStoreTests.cs
    Api/
        ReportControllerTests.cs
        TemplatesControllerTests.cs
    Integration/
        RenderEndpointsTests.cs
```

### 5. Main methods to cover

| Area | Class | Method | What to validate |
|------|-------|--------|------------------|
| Engine | `TemplateEngine` | `RenderAsync` | Returns PDF bytes for `FullClass` and `Builder` modes; invalid template raises compilation error |
| Engine | `TemplateEngine` | `RenderTemplateAsync` | Uses `TemplateRecord.Mode` and renders with provided data |
| Engine | `TemplateEngine` | `ConvertToDynamic` / `JsonElementToExpando` | Converts JSON payload into dynamic object preserving primitive values and object structure |
| Store | `InMemoryTemplateStore` | `SaveAsync` | Assigns `Id` when empty and updates `UpdatedAt` on update |
| Store | `InMemoryTemplateStore` | `GetAsync` / `ListAsync` / `DeleteAsync` | CRUD flow and delete return value when item does not exist |
| API | `ReportController` | `Render` | Returns file response and default filename when request fileName is empty |
| API | `ReportController` | `RenderById` | Returns `NotFound` for unknown id and uses template defaults when request is null |
| API | `ReportController` | `Preview` | Returns `BadRequest` when `MockData` is null and file response when configured |
| API | `TemplatesController` | `List` / `Get` / `Create` / `Update` / `Delete` | HTTP status codes and payloads for success and not-found scenarios |

### 6. Example: starter test for TemplateEngine

```csharp
using Buelo.Contracts;
using Buelo.Engine;
using FluentAssertions;

namespace Buelo.Tests.Engine;

public class TemplateEngineTests
{
        [Fact]
        public async Task RenderAsync_BuilderMode_ShouldGeneratePdfBytes()
        {
                // Arrange
                var engine = new TemplateEngine(new DefaultHelperRegistry());
                var template = "Document.Create(c => c.Page(p => p.Content().Text((string)data.name))).GeneratePdf()";

                // Act
                var result = await engine.RenderAsync(
                        template,
                        new { name = "Test" },
                        TemplateMode.Builder);

                // Assert
                result.Should().NotBeNull();
                result.Should().NotBeEmpty();
        }
}
```

### 7. Run all tests

```bash
dotnet test Buelo.slnx --collect:"XPlat Code Coverage"
```

---

## License

MIT — see [LICENSE.txt](LICENSE.txt).
