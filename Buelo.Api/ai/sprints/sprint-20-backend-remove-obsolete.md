# Sprint 20 — Backend: Remove Obsolete Functionality

## Goal
The system has been `.buelo`-only since Sprint 14. `TemplateMode.Sections`, `TemplateMode.Partial`,
`TemplateMode.FullClass`, and `TemplateMode.Builder` are dead code. The Roslyn C# compilation path,
`SectionsTemplateParser`, and the bundle ZIP import/export are no longer needed. Remove all of it.

> **Note:** No breaking-change risk. The system is not in production and no external consumers exist.

## Status
`[x] done`

## Dependencies
- Sprint 18 backend complete ✅ (`@project` directive; `OutputFormat` per template)

---

## Tasks

### 20-B.1 — Simplify `TemplateMode` enum

File: `Buelo.Contracts/TemplateMode.cs`

Keep only:

```csharp
public enum TemplateMode
{
    BueloDsl = 3,   // preserve existing integer value to avoid JSON/DB migration issues
}
```

Remove: `Sections`, `Partial`, `FullClass`, `Builder`.

---

### 20-B.2 — Remove `SectionsTemplateParser`

File to delete: `Buelo.Engine/SectionsTemplateParser.cs`

---

### 20-B.3 — Remove Roslyn compilation path from `TemplateEngine`

File: `Buelo.Engine/TemplateEngine.cs`

Remove all branches that handle `TemplateMode.Sections`, `TemplateMode.Partial`, or Roslyn
`CSharpCompilation`. The engine now exclusively delegates to `BueloDslEngine`.

Remove NuGet reference to `Microsoft.CodeAnalysis.CSharp` if it is only used by the removed path.
Check `Buelo.Engine.csproj` for the package reference and remove if no longer needed.

---

### 20-B.4 — Remove bundle ZIP export/import from `TemplatesController`

File: `Buelo.Api/Controllers/TemplatesController.cs`

Remove:
- `GET /api/templates/{id}/export` (ZIP download)
- `POST /api/templates/import` (ZIP upload)

These endpoints added complexity that is no longer warranted. Template files are now managed
directly via the file system by the workspace tools.

Remove `using System.IO.Compression` if no longer used.

---

### 20-B.5 — Remove `TemplateMode` guard from `ExcelRenderer`

File: `Buelo.Engine/Renderers/ExcelRenderer.cs`

Remove the `SupportsMode` check for `Sections` (the renderer now only needs to handle `BueloDsl`).
Update the `SupportsMode` implementation:

```csharp
public bool SupportsMode(TemplateMode mode) => mode == TemplateMode.BueloDsl;
```

---

### 20-B.6 — Remove `IReport` interface (if unused)

File: `Buelo.Contracts/IReport.cs`

Check whether `IReport` is referenced anywhere. If it was only used by the old Sections/FullClass
Roslyn pipeline, delete the file.

---

### 20-B.7 — Remove `InMemoryBueloProjectStore` (covered by Sprint 18 but verify)

File: `Buelo.Engine/InMemoryBueloProjectStore.cs` — should have been deleted in Sprint 18.
If still present, delete now.

---

### 20-B.8 — Clean up `TemplateRecord.Mode` default

File: `Buelo.Contracts/TemplateRecord.cs`

Update the default:

```csharp
public TemplateMode Mode { get; set; } = TemplateMode.BueloDsl;
```

---

### 20-B.9 — Update all tests that reference removed modes

Files: `Buelo.Tests/Engine/*.cs` and `Buelo.Tests/Api/*.cs`

- Remove any test that exercises `Sections` or `Partial` template mode.
- Update helper methods that create `TemplateRecord` fixtures to use `TemplateMode.BueloDsl`.

---

### 20-B.10 — Update `TemplateHeaderParser` (if applicable)

File: `Buelo.Engine/TemplateHeaderParser.cs`

If `TemplateHeaderParser` only parses C# `//` directive comments for the old Sections mode,
evaluate whether it is still needed. If it is now dead code, delete it and remove its usages.

---

## Files / Code to Delete

| Item | Type |
|------|------|
| `SectionsTemplateParser.cs` | file |
| `IReport.cs` (if unused) | file |
| `InMemoryBueloProjectStore.cs` | file (if not removed in Sprint 18) |
| `TemplateHeaderParser.cs` | file (if only used by Sections mode) |
| `TemplateMode.Sections/Partial/FullClass/Builder` | enum members |
| Roslyn `CSharpCompilation` block in `TemplateEngine` | code block |
| ZIP export/import endpoints in `TemplatesController` | methods |
| `Microsoft.CodeAnalysis.CSharp` NuGet reference | csproj entry (if unused) |
