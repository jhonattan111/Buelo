# TASKS.md — Buelo Backend

## Overview
Source of truth for backend sprint planning. Each sprint has its own file in `ai/sprints/`.

## Sprint Index

| Sprint | File | Goal | Status |
|--------|------|------|--------|
| 6 | [sprint-6-sections-mode.md](sprints/sprint-6-sections-mode.md) | Add TemplateMode.Sections and Partial, SectionsTemplateParser, @import directive | `[x] done` |
| 7 | [sprint-7-backend-dsl-foundation.md](sprints/sprint-7-backend-dsl-foundation.md) | Deprecate FullClass/Builder, header directives (@data/@settings/@schema), /validate endpoint | `[x] done` |
| 8 | [sprint-8-backend-template-bundle.md](sprints/sprint-8-backend-template-bundle.md) | TemplateBundle artefacts, FileSystemTemplateStore, export/import ZIP | `[x] done` |
| 9 | [sprint-9-backend-helpers-versioning.md](sprints/sprint-9-backend-helpers-versioning.md) | Dynamic @helper scripts, template versioning + restore | `[x] done` |
| 13 | [sprint-13-backend-global-artefact-store.md](sprints/sprint-13-backend-global-artefact-store.md) | Global shared files (colaborador.json, formatters.csx) not tied to any template; file extension conventions | `[x] done` |
| 14 | [sprint-14-backend-buelo-dsl-redesign.md](sprints/sprint-14-backend-buelo-dsl-redesign.md) | YAML-like .buelo component DSL (BueloDslParser, BueloDslCompiler, TemplateMode.BueloDsl) | `[x] done` |
| 15 | [sprint-15-backend-project-file.md](sprints/sprint-15-backend-project-file.md) | project.bueloproject — workspace settings, global mock data, page defaults cascade | `[x] done` |
| 16 | [sprint-16-backend-file-validation.md](sprints/sprint-16-backend-file-validation.md) | Per-file-type validation: .buelo DSL, .json, .cs/.csx via Roslyn syntax check | `[x] done` |
| 17 | [sprint-17-backend-extensible-renderers.md](sprints/sprint-17-backend-extensible-renderers.md) | IOutputRenderer abstraction; PdfRenderer + ExcelRenderer (ClosedXML); ?format= param | `[x] done` |
| 18 | [sprint-18-backend-inline-project-config.md](sprints/sprint-18-backend-inline-project-config.md) | Remove project.bueloproject; @project directive in .buelo; OutputFormat per TemplateRecord | `[x] done` |
| 19 | [sprint-19-backend-project-validation.md](sprints/sprint-19-backend-project-validation.md) | POST /api/validate/project — full workspace validation | `[x] done` |
| 20 | [sprint-20-backend-remove-obsolete.md](sprints/sprint-20-backend-remove-obsolete.md) | Remove Sections/Partial modes, Roslyn path, SectionsTemplateParser, ZIP bundle endpoints | `[x] done` |
| 21 | [sprint-21-backend-workspace-filesystem.md](sprints/sprint-21-backend-workspace-filesystem.md) | Workspace filesystem APIs (folders/files), render by .buelo path, dataSourcePath binding, import resolver, remove global artefact dependency | `[x] done` |

## Dependency Chain

```
Sprint 6 (Sections Mode)
    ↓
Sprint 7 (DSL Foundation)
    ↓
Sprint 8 (Template Bundle)
    ↓
Sprint 9 (Helpers + Versioning)
    ↓
[Frontend sprints 10–12 — see BueloWeb/ai/TASKS.md]
    ↓
Sprint 13 (Global Artefact Store)        ← file extension conventions established here
    ↓
Sprint 14 (.buelo DSL Redesign)          ← BueloDslParser + BueloDslCompiler
    ↓
Sprint 15 (Project File)                 ← settings cascade; unblocks frontend Sprint 15
    ↓
Sprint 16 (Per-file Validation)          ← unblocks frontend Sprint 16
    ↓
Sprint 17 (Extensible Renderers)         ← Excel; unblocks frontend Sprint 17
    ↓
Sprint 18 (Inline @project + OutputFormat)  ← removes project.bueloproject; format at creation
    ↓
Sprint 19 (Project-wide Validation)      ← replaces per-file validate trigger
    ↓
Sprint 20 (Remove Obsolete)              ← clean .buelo-only codebase
    ↓
Sprint 21 (Workspace Filesystem + Imports + Data Source Binding)
    ↓
[Frontend sprint 22 — see BueloWeb/ai/TASKS.md]
```

## Project Structure (reference)

```
Buelo.Contracts/        ← shared interfaces, models, enums (no business logic)
Buelo.Engine/           ← Roslyn compiler, PDF generation, template store implementations
Buelo.Api/              ← ASP.NET Core controllers and startup
  Controllers/
    ReportController.cs
    TemplatesController.cs
  Program.cs
Buelo.Tests/
  Engine/               ← unit tests for engine components
  Api/                  ← controller-level tests
```

## Conventions

- Each sprint modifies **Contracts → Engine → Api** in that order (dependency direction)
- Unit tests live in `Buelo.Tests/` alongside the layer being tested
- No breaking changes to existing API contracts unless sprint explicitly states otherwise
- After completing any sprint task, run `dotnet build` and `dotnet test` before marking done
