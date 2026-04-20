# Sprint 13 — Backend: Global Artefact Store (Shared Files)

## Goal
Allow files to exist **independently from any specific template**. A `colaborador.json` data file, a `shared-header.buelo` partial, or a `formatters.csx` helper can be created once and referenced by any report, eliminating duplication across templates.

## Status
`[ ] pending`

## Dependencies
- Sprint 9 complete ✅
- `FileSystemTemplateStore` implemented ✅

---

## File Extension Conventions (established in this sprint)

| Type | Extension | Description |
|------|-----------|-------------|
| Report / Partial template | `.buelo` | Main report or reusable partial |
| Helper scripts | `.csx` or `.cs` | C# helper functions |
| Data files | `.json` | JSON data for mock or real data binding |
| Project config | `.bueloproject` | Workspace-level settings (see Sprint 15) |

These conventions apply to both global artefacts and per-template artefacts. Template engine resolves files by extension to determine their type.

---

## Backend Scope

### 13.1 — `GlobalArtefact` model

File: `Buelo.Contracts/GlobalArtefact.cs`

```csharp
public class GlobalArtefact
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;       // slug-safe, e.g. "colaborador"
    public string Extension { get; set; } = string.Empty;  // e.g. ".json", ".buelo", ".csx"
    public string Content { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IList<string> Tags { get; set; } = [];          // for filtering/search in UI
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Full file name = `Name + Extension`, e.g. `colaborador.json`, `shared-header.buelo`, `formatters.csx`.

---

### 13.2 — `IGlobalArtefactStore` interface

File: `Buelo.Contracts/IGlobalArtefactStore.cs`

```csharp
public interface IGlobalArtefactStore
{
    Task<GlobalArtefact?> GetAsync(Guid id);
    Task<GlobalArtefact?> GetByNameAsync(string name, string extension);
    Task<IReadOnlyList<GlobalArtefact>> ListAsync(string? extensionFilter = null);
    Task<GlobalArtefact> SaveAsync(GlobalArtefact artefact);   // creates if Id == Guid.Empty
    Task<bool> DeleteAsync(Guid id);
}
```

---

### 13.3 — `InMemoryGlobalArtefactStore`

File: `Buelo.Engine/InMemoryGlobalArtefactStore.cs`

Implements `IGlobalArtefactStore` using a `ConcurrentDictionary<Guid, GlobalArtefact>`.  
Auto-assigns `Id`, `CreatedAt`, and `UpdatedAt` in `SaveAsync`.  
`GetByNameAsync`: case-insensitive match on both `Name` and `Extension`.

---

### 13.4 — `FileSystemGlobalArtefactStore`

File: `Buelo.Engine/FileSystemGlobalArtefactStore.cs`

Implements `IGlobalArtefactStore`. Stores global artefacts in a flat directory:

```
{root}/_global/
  colaborador.json
  colaborador.json.meta.json     ← { id, description, tags, createdAt, updatedAt }
  shared-header.buelo
  shared-header.buelo.meta.json
  formatters.csx
  formatters.csx.meta.json
```

- Root directory: `IConfiguration["Buelo:TemplateStorePath"]` (same root as `FileSystemTemplateStore`)
- `_global/` subfolder is reserved and cannot be used as a template ID
- `GetByNameAsync`: scans `_global/` directory for matching `{name}{extension}` file
- `ListAsync(extensionFilter)`: returns all artefacts, optionally filtered by extension

---

### 13.5 — Resolution order in `TemplateEngine`

File: `Buelo.Engine/TemplateEngine.cs`

When resolving a `@data from "ref"`, `@import ... from "ref"`, or `@helper from "ref"` directive:

1. **Local artefact**: search `TemplateRecord.Artefacts` by name (exact match)
2. **Global artefact by name**: call `IGlobalArtefactStore.GetByNameAsync(name, expectedExtension)`
3. **Global artefact by GUID**: if `ref` is a valid GUID, call `IGlobalArtefactStore.GetAsync(Guid.Parse(ref))`
4. **Fallback**: request data / MockData

Inject `IGlobalArtefactStore` into `TemplateEngine` via constructor DI.

---

### 13.6 — CRUD API endpoints

File: `Buelo.Api/Controllers/GlobalArtefactsController.cs`

New controller:

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/artefacts` | List all global artefacts (optional `?extension=.json` filter) |
| `GET` | `/api/artefacts/{id}` | Get single artefact with content |
| `GET` | `/api/artefacts/by-name/{name}` | Get by name+extension (query: `?extension=.json`) |
| `POST` | `/api/artefacts` | Create new global artefact |
| `PUT` | `/api/artefacts/{id}` | Update artefact (content, description, tags) |
| `DELETE` | `/api/artefacts/{id}` | Delete artefact |

Request body for POST/PUT:
```json
{
  "name": "colaborador",
  "extension": ".json",
  "content": "{ \"nome\": \"João\" }",
  "description": "Dados de colaborador para testes",
  "tags": ["rh", "mock"]
}
```

---

### 13.7 — DI registration

File: `Buelo.Engine/EngineExtensions.cs`

```csharp
// Add to AddBueloEngine()
services.AddSingleton<IGlobalArtefactStore, InMemoryGlobalArtefactStore>();
// Overridden to FileSystemGlobalArtefactStore when FileSystem mode is configured
```

---

## Tests

File: `Buelo.Tests/Engine/GlobalArtefactStoreTests.cs`

- `SaveAndRetrieve_ById_ReturnsArtefact`
- `SaveAndRetrieve_ByName_ReturnsArtefact_CaseInsensitive`
- `ListWithExtensionFilter_ReturnsOnlyMatchingType`
- `Delete_ExistingArtefact_ReturnsTrue`
- `Delete_NonExistent_ReturnsFalse`

File: `Buelo.Tests/Engine/TemplateEngineTests.cs` (additions)

- `DataResolution_LocalArtefactTakesPrecedenceOverGlobal`
- `DataResolution_FallsBackToGlobalWhenLocalMissing`
- `ImportResolution_ResolvesByGuidFromGlobalStore`
