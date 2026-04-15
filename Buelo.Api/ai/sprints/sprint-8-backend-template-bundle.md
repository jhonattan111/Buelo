# Sprint 8 — Backend: Template Bundle & File-Scoped Artefacts

## Goal
Introduce the concept of a **TemplateBundle** — a report is composed of multiple named artefacts (template source, mock data, data schema, helpers) stored and managed individually. The `TemplateRecord` evolves to support multi-artefact storage. A `FileSystemTemplateStore` is added as an optional persistence backend.

## Status
`[ ] pending`

## Dependencies
- Sprint 7 complete ✅
- `TemplateHeader`, `TemplateHeaderParser` implemented

---

## Backend Scope

### 8.1 — Add `Artefacts` collection to `TemplateRecord`

File: `Buelo.Contracts/TemplateRecord.cs`

```csharp
public class TemplateRecord
{
    // existing fields stay unchanged ...

    /// <summary>Named artefacts attached to this template.</summary>
    public IList<TemplateArtefact> Artefacts { get; set; } = [];
}

public class TemplateArtefact
{
    public string Name { get; set; } = string.Empty;  // e.g. "mockdata", "schema", "helper-tax"
    public string Extension { get; set; } = string.Empty; // ".json", ".cs", ".schema.json"
    public string Content { get; set; } = string.Empty;
}
```

Backward compatible: existing records without artefacts have an empty list.

### 8.2 — Artefact resolution in `TemplateEngine`

File: `Buelo.Engine/TemplateEngine.cs`

When `TemplateHeader.DataRef` is set:
1. Try resolving as an artefact name within the same `TemplateRecord` (match `Name` or `Name + Extension`)
2. If not found, try `ITemplateStore.GetAsync(Guid.Parse(ref))` for cross-template data
3. Fallback to `ReportRequest.Data` if still null
4. Throw `InvalidOperationException` only if all three resolve to null and no `MockData` exists

### 8.3 — Artefact CRUD endpoints

File: `Buelo.Api/Controllers/TemplatesController.cs`

New endpoints:

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/templates/{id}/artefacts` | List artefacts (name + extension, no content) |
| `GET` | `/api/templates/{id}/artefacts/{name}` | Get single artefact with content |
| `PUT` | `/api/templates/{id}/artefacts/{name}` | Upsert artefact (body: `{ extension, content }`) |
| `DELETE` | `/api/templates/{id}/artefacts/{name}` | Delete artefact |

- `name` in path is slug-safe (lowercase, hyphens only)
- Extension stored separately from name, not embedded in path

### 8.4 — `FileSystemTemplateStore`

File: `Buelo.Engine/FileSystemTemplateStore.cs`

Implements `ITemplateStore`. Each template is a folder:

```
{root}/{id}/
  template.record.json      ← TemplateRecord metadata (no Template/Artefacts fields)
  template.report.cs        ← TemplateRecord.Template content
  {artefact-name}.{ext}     ← each TemplateArtefact
```

- Root directory configured via `IConfiguration["Buelo:TemplateStorePath"]`
- `SaveAsync`: writes/updates folder; artefacts written as individual files
- `GetAsync`: reads folder, reconstructs `TemplateRecord`
- `ListAsync`: enumerates subdirectories
- `DeleteAsync`: deletes folder with all contents after confirmation (`Directory.Delete(path, recursive: true)`)
- Registration opt-in via `builder.Services.AddBueloFileSystemStore()` extension

### 8.5 — Export / Import endpoints

File: `Buelo.Api/Controllers/TemplatesController.cs`

```
GET  /api/templates/{id}/export   → application/zip
POST /api/templates/import        → multipart/form-data (zip file)
```

Export zip structure mirrors FileSystemTemplateStore folder layout, making it Git-friendly.

### 8.6 — Unit Tests

File: `Buelo.Tests/Engine/FileSystemTemplateStoreTests.cs`

- CRUD round-trip: save → get → matches original
- Artefact upsert: second `PUT` with same name updates content
- `ListAsync` returns all saved records
- `DeleteAsync` cleans up directory

---

## Acceptance Criteria
- [ ] `TemplateRecord` has `Artefacts` list, backward compatible (empty = no artefacts)
- [ ] Artefact CRUD endpoints functional
- [ ] `@data from "mockdata"` resolves from same-template artefact during render
- [ ] `FileSystemTemplateStore` passes round-trip tests
- [ ] Export ZIP contains all artefacts; re-import restores template with artefacts intact
- [ ] `InMemoryTemplateStore` still the default; `FileSystemTemplateStore` is opt-in
