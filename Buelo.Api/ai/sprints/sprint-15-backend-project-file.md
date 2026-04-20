# Sprint 15 — Backend: Project File & Workspace Settings

## Goal
Introduce a **project-level configuration file** (`.bueloproject`) that stores workspace-wide defaults: page settings, global mock data, and project metadata. The API exposes a `/api/project` endpoint for reading and updating this configuration. Settings cascade: project defaults → template record defaults → per-request overrides.

## Status
`[ ] pending`

## Dependencies
- Sprint 13 complete ✅ (global artefact store)
- Sprint 14 complete ✅ (BueloDsl mode, file extension conventions)

---

## Project File Format

File name: `project.bueloproject` (stored at `{TemplateStorePath}/project.bueloproject`)

```json
{
  "name": "My Report Project",
  "description": "HR reports for ACME Corp",
  "version": "1.0.0",
  "pageSettings": {
    "pageSize": "A4",
    "marginHorizontal": 2.0,
    "marginVertical": 2.0,
    "backgroundColor": "#FFFFFF",
    "defaultTextColor": "#000000",
    "defaultFontSize": 12,
    "showHeader": true,
    "showFooter": true,
    "watermarkText": null
  },
  "mockData": {
    "empresa": "ACME Corp",
    "dataGeracao": "2026-04-20T00:00:00Z"
  },
  "defaultOutputFormat": "pdf",
  "createdAt": "2026-04-20T00:00:00Z",
  "updatedAt": "2026-04-20T00:00:00Z"
}
```

---

## Backend Scope

### 15.1 — `BueloProject` model

File: `Buelo.Contracts/BueloProject.cs`

```csharp
public class BueloProject
{
    public string Name { get; set; } = "Buelo Project";
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0.0";
    public PageSettings PageSettings { get; set; } = new();
    public object? MockData { get; set; }
    public string DefaultOutputFormat { get; set; } = "pdf";  // "pdf" | "excel"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

---

### 15.2 — `IBueloProjectStore` interface

File: `Buelo.Contracts/IBueloProjectStore.cs`

```csharp
public interface IBueloProjectStore
{
    Task<BueloProject> GetAsync();
    Task<BueloProject> SaveAsync(BueloProject project);
}
```

Single-project model: always one project per workspace. `GetAsync` returns defaults if not yet created.

---

### 15.3 — `FileSystemBueloProjectStore`

File: `Buelo.Engine/FileSystemBueloProjectStore.cs`

- Reads/writes `{TemplateStorePath}/project.bueloproject` as JSON
- `GetAsync`: returns defaults (`new BueloProject()`) if file does not exist
- `SaveAsync`: sets `UpdatedAt = DateTimeOffset.UtcNow`, writes file atomically (write-to-temp then rename)
- Thread-safe: uses `SemaphoreSlim(1)`

---

### 15.4 — `InMemoryBueloProjectStore`

File: `Buelo.Engine/InMemoryBueloProjectStore.cs`

- In-memory implementation for testing and dev mode
- Starts with default `BueloProject` values

---

### 15.5 — Settings cascade in `TemplateEngine`

File: `Buelo.Engine/TemplateEngine.cs`

Merge order (later values override earlier):

```
1. BueloProject.PageSettings          ← workspace defaults
2. TemplateRecord.PageSettings        ← per-template overrides
3. ReportRequest.PageSettings         ← per-request overrides (existing behavior)
```

Inject `IBueloProjectStore` into `TemplateEngine` constructor. Load project settings at start of `RenderAsync`.

Merge helper (new):
```csharp
internal static PageSettings MergeSettings(
    PageSettings project,
    PageSettings? template,
    PageSettings? request)
```
For each nullable field: use first non-null value from request → template → project.

---

### 15.6 — Project API endpoint

File: `Buelo.Api/Controllers/ProjectController.cs`

New controller:

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/project` | Get current project settings |
| `PUT` | `/api/project` | Update project settings (full replace) |
| `PATCH` | `/api/project/page-settings` | Update only page settings |
| `PATCH` | `/api/project/mock-data` | Update only mock data |
| `GET` | `/api/project/reset` | Reset to factory defaults |

`PATCH` endpoints accept partial updates — only provided fields are changed (use `JsonMergePatch` or manual null-check pattern).

---

### 15.7 — DI registration

File: `Buelo.Engine/EngineExtensions.cs`

```csharp
services.AddSingleton<IBueloProjectStore, InMemoryBueloProjectStore>();
// Overridden to FileSystemBueloProjectStore when file system mode is active
```

---

## Tests

File: `Buelo.Tests/Engine/BueloProjectStoreTests.cs`
- `GetAsync_WhenNoFileExists_ReturnsDefaults`
- `SaveAndGet_PersistsAllFields`
- `SaveAsync_UpdatesTimestamp`

File: `Buelo.Tests/Engine/SettingsCascadeTests.cs`
- `MergeSettings_RequestOverridesTemplate_TemplateOverridesProject`
- `MergeSettings_NullRequest_UsesTemplateSettings`
- `MergeSettings_AllNull_UsesProjectDefaults`

File: `Buelo.Tests/Api/ProjectControllerTests.cs`
- `GetProject_ReturnsCurrentSettings`
- `PutProject_UpdatesAllFields`
- `PatchPageSettings_UpdatesOnlyPageSettings`
