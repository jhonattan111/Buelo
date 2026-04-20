# Sprint 21 (Backend) - Workspace Filesystem + Imports + Data Source Binding

## Goal

Move backend from template-centric/global-artefact-centric model to a real workspace filesystem model
(folder + file), enabling:

1. nested folders and file creation (VS Code-like tree support)
2. rendering from a selected `.buelo` file path
3. binding a created `.json` file as test data source for a `.buelo` file
4. deterministic import resolution between workspace files
5. removal of `global artefact` as a first-class concept

## Status

`[x] done`

## Dependencies

- Sprint 20 backend complete
- Frontend Sprint 21 complete

---

## Scope

### In scope

- New workspace tree/file APIs
- New engine store abstraction for folder/file operations
- Import resolver for `.buelo` references
- Report settings persistence for `dataSourcePath`
- Rendering/validation by file path

### Out of scope

- Role-based permissions
- External package registry/import from URLs
- Multi-workspace tenancy

---

## Tasks

### BE-21.1 - Add workspace filesystem contracts

Files:

- `Buelo.Contracts/IWorkspaceStore.cs` (new)
- `Buelo.Contracts/WorkspaceNode.cs` (new)
- `Buelo.Contracts/WorkspaceFileRecord.cs` (new)
- `Buelo.Contracts/PageSettings.cs` (extend)
- `Buelo.Contracts/ReportRequest.cs` (extend)

Changes:

- Define folder/file primitives independent of template ids.
- Add `dataSourcePath?: string` under report settings for a `.buelo` file.
- Add render request option by file path:
  - `templatePath` (required for file-based render)
  - `dataSourcePath` (optional override)

Notes:

- Keep current DTOs temporarily for compatibility where needed.

---

### BE-21.2 - Implement filesystem workspace store

Files:

- `Buelo.Engine/FileSystemWorkspaceStore.cs` (new)
- `Buelo.Engine/EngineExtensions.cs`

Changes:

- CRUD for folders/files with path normalization and traversal protection.
- Only workspace-relative paths are accepted.
- Prevent escaping root (`..`, absolute paths, mixed separators abuse).
- Expose read/write/list/move/rename/delete primitives.

---

### BE-21.3 - Remove global artefact dependency from render path

Files:

- `Buelo.Engine/TemplateEngine.cs`
- `Buelo.Engine/DefaultHelperRegistry.cs`
- `Buelo.Api/Controllers/GlobalArtefactsController.cs` (deprecate/remove)
- `Buelo.Contracts/IGlobalArtefactStore.cs` (deprecate)

Changes:

- Replace global artefact lookup with workspace file lookup by path/extension.
- `.json`, `.cs`, `.csx` are regular files in folders.
- Keep migration compatibility only if required by existing persisted data.

---

### BE-21.4 - Import resolver for workspace paths

Files:

- `Buelo.Engine/BueloDsl/BueloImportResolver.cs` (new)
- `Buelo.Engine/BueloDsl/BueloDslCompiler.cs`
- `Buelo.Engine/BueloDsl/BueloDslParser.cs` (if needed)

Rules:

- `import "./partials/header.buelo"` => relative to current file.
- `import "/reports/shared/table.buelo"` => workspace-root absolute path.
- Allowed import targets: `.buelo`, `.json`, `.cs`, `.csx`.
- Detect import cycles and return actionable diagnostics with path chain.
- Normalize separators and case handling according to platform behavior.

---

### BE-21.5 - Render from `.buelo` file path + bound data source

Files:

- `Buelo.Api/Controllers/ReportController.cs`
- `Buelo.Engine/TemplateEngine.cs`

Changes:

- New endpoint (or expanded existing endpoint) to render by workspace file path.
- Resolve active `.buelo` file.
- Resolve test data from:
  1. request override `dataSourcePath`
  2. file report settings `dataSourcePath`
  3. inline/default empty object
- Fail with clear error when bound `.json` does not exist or is invalid JSON.

---

### BE-21.6 - Workspace tree + file operations API

Files:

- `Buelo.Api/Controllers/WorkspaceController.cs` (new)

Endpoints:

- `GET /api/workspace/tree`
- `POST /api/workspace/folders`
- `POST /api/workspace/files`
- `GET /api/workspace/files/content?path=...`
- `PUT /api/workspace/files/content`
- `PATCH /api/workspace/files/move`
- `PATCH /api/workspace/files/rename`
- `DELETE /api/workspace/nodes?path=...`

Requirements:

- Return stable path-based identities.
- Include `kind` inference by extension for frontend UX.

---

### BE-21.7 - Project-wide validation over folders/files

Files:

- `Buelo.Api/Controllers/ValidateController.cs`
- `Buelo.Engine/Validators/*`

Changes:

- Validate by scanning workspace files recursively.
- Keep per-extension validation behavior (`.buelo`, `.json`, `.cs`, `.csx`).
- Validation payload must include canonical `path` for each result.

---

### BE-21.8 - Compatibility and migration safeguards

Files:

- `Buelo.Engine/FileSystemTemplateStore.cs`
- `Buelo.Api/Program.cs`

Changes:

- Add migration adapter (if old template records exist) mapping to workspace-root `.buelo` files.
- Mark deprecated endpoints with sunset comments and compatibility window.

---

## Acceptance Criteria

1. Backend can create nested folders and files of types `.buelo`, `.json`, `.cs`, `.csx`, and generic files.
2. Render endpoint accepts a `.buelo` file path and renders correctly.
3. A `.buelo` file can reference a saved `.json` via report settings `dataSourcePath`.
4. Import resolution supports relative and workspace-root absolute imports with cycle detection.
5. Global artefact is no longer required for normal workflow.
6. Validation returns folder-aware, path-based results.

---

## Tests

- Unit tests (`Buelo.Tests/Engine`):
  - path normalization and traversal protection
  - import resolution and cycle detection
  - data source binding precedence
- API tests (`Buelo.Tests/Api`):
  - workspace CRUD endpoints
  - render by file path
  - validation returns canonical paths

Mandatory commands after implementation:

- `dotnet build`
- `dotnet test`
