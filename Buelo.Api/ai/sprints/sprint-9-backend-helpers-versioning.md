# Sprint 9 — Backend: Dynamic Helpers & Template Versioning

## Goal
Enable per-template helper scripts (extending `IHelperRegistry` dynamically), and introduce template versioning so historical snapshots are preserved on every save. Closes the backend feature roadmap before frontend adaption sprints.

## Status
`[x] done`

## Dependencies
- Sprint 8 complete ✅
- `TemplateHeader`, `TemplateHeaderParser`, `TemplateArtefact` implemented

---

## Backend Scope

### 9.1 — Runtime helper injection from `@helper` directives

File: `Buelo.Engine/TemplateEngine.cs`

When `TemplateHeader.Helpers` is non-empty:
1. Generate a C# helper class source that wraps each helper as a static method
2. Include the generated class in the Roslyn `ScriptOptions.References` + add `using static BueloGeneratedHelpers;` to the wrapper
3. Each helper is callable by name inside the template body (e.g. `FormatCNPJ(data.cnpj)`)
4. Helper compilation is cached separately by a hash of all helper signatures + bodies (not the full template hash)

Example generated class:

```csharp
public static class BueloGeneratedHelpers
{
    public static string FormatCNPJ(string value) => value.Insert(2, ".");
    public static string FormatCPF(string value) => value.Insert(3, ".");
}
```

### 9.2 — `@helper` artefact type

File: `Buelo.Engine/TemplateEngine.cs` + `TemplateHeaderParser.cs`

Also support declaring helpers as a separate artefact (extension `.helpers.cs`) instead of inline `@helper` lines:

```csharp
@helper from "tax-helpers"   // resolves TemplateArtefact with name "tax-helpers" and extension ".helpers.cs"
```

Parser:
- `@helper from "<name>"` → `TemplateHeader.HelperArtefactRef = name`
- Inline `@helper Name(...) => ...;` → `TemplateHeader.Helpers` list (Sprint 7 path)

### 9.3 — Template versioning

File: `Buelo.Contracts/TemplateVersion.cs`

```csharp
public class TemplateVersion
{
    public int Version { get; set; }
    public string Template { get; set; } = string.Empty;
    public IList<TemplateArtefact> Artefacts { get; set; } = [];
    public DateTimeOffset SavedAt { get; set; }
    public string? SavedBy { get; set; } // reserved for auth, nullable
}
```

File: `Buelo.Contracts/ITemplateStore.cs`

Add optional versioning methods with default implementations that return empty:

```csharp
Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(Guid id) 
    => Task.FromResult<IReadOnlyList<TemplateVersion>>([]);

Task<TemplateVersion?> GetVersionAsync(Guid id, int version) 
    => Task.FromResult<TemplateVersion?>(null);
```

File: `Buelo.Engine/InMemoryTemplateStore.cs` + `FileSystemTemplateStore.cs`

- On every `SaveAsync`, snapshot current `Template` + `Artefacts` into version history before overwriting
- `InMemoryTemplateStore`: keeps last 20 versions per template in memory
- `FileSystemTemplateStore`: writes `versions/{n}.snapshot.json` inside the template folder

### 9.4 — Version endpoints

File: `Buelo.Api/Controllers/TemplatesController.cs`

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/templates/{id}/versions` | List version numbers + timestamps |
| `GET` | `/api/templates/{id}/versions/{n}` | Get specific snapshot (full template + artefacts) |
| `POST` | `/api/templates/{id}/versions/{n}/restore` | Overwrite current template from snapshot (creates new version) |

### 9.5 — Render from version

File: `Buelo.Api/Controllers/ReportController.cs`

New optional query param on existing endpoint:

```
POST /api/report/render/{id}?version=3
```

Resolves template from version snapshot instead of current.

### 9.6 — Unit Tests

File: `Buelo.Tests/Engine/`

- `DynamicHelpersTests.cs`: inline `@helper` callable inside Sections template
- `TemplateVersioningTests.cs`: save twice → 2 versions stored; restore rewinds content

---

## Acceptance Criteria
- [x] Inline `@helper` functions callable inside Sections template without modifying `DefaultHelperRegistry`
- [x] `@helper from "artefact-name"` loads helpers from a `.helpers.cs` artefact
- [x] Every `SaveAsync` creates a version snapshot
- [x] Version history retrievable via GET endpoints
- [x] `POST .../restore` overwrites current template and creates a new version entry
- [x] `POST /api/report/render/{id}?version=N` renders historical snapshot
