# TASKS.md — Buelo Backend

## Overview
Source of truth for backend sprint planning. Each sprint has its own file in `ai/sprints/`.

> **Arquitetura atual**: Templates C# puros implementando `IDocument` do QuestPDF. A DSL customizada (.buelo) foi removida. Consulte `ARCHITECTURE.md` na raiz para a visão completa.

## Sprint Index

### ✅ Sprints Arquivadas (DSL era — removida)

| Sprint | File | Goal | Status |
|--------|------|------|--------|
| 6 | _archived/sprint-6-sections-mode.md | Sections/Partial modes, @import directive | `[x] archived` |
| 7 | _archived/sprint-7-backend-dsl-foundation.md | DSL foundation, @data/@settings directives | `[x] archived` |
| 8 | [sprint-8-backend-template-bundle.md](sprints/sprint-8-backend-template-bundle.md) | TemplateBundle artefacts, FileSystemTemplateStore, export/import ZIP | `[x] done` |
| 9 | [sprint-9-backend-helpers-versioning.md](sprints/sprint-9-backend-helpers-versioning.md) | Dynamic @helper scripts, template versioning + restore | `[x] done` |
| 13 | [sprint-13-backend-global-artefact-store.md](sprints/sprint-13-backend-global-artefact-store.md) | Global shared files (colaborador.json, formatters.csx); file extension conventions | `[x] done` |
| 14 | _archived/sprint-14-backend-buelo-dsl-redesign.md | YAML-like .buelo DSL (BueloDslParser, BueloDslCompiler) | `[x] archived` |
| 15 | [sprint-15-backend-project-file.md](sprints/sprint-15-backend-project-file.md) | project.bueloproject — workspace settings, global mock data, page defaults cascade | `[x] done` |
| 16 | [sprint-16-backend-file-validation.md](sprints/sprint-16-backend-file-validation.md) | Per-file-type validation: .json, .cs/.csx via Roslyn syntax check | `[x] done` |
| 17 | [sprint-17-backend-extensible-renderers.md](sprints/sprint-17-backend-extensible-renderers.md) | IOutputRenderer abstraction; PdfRenderer + ExcelRenderer (ClosedXML); ?format= param | `[x] done` |
| 18 | [sprint-18-backend-inline-project-config.md](sprints/sprint-18-backend-inline-project-config.md) | OutputFormat per TemplateRecord; PageSettings cascade | `[x] done` |
| 19 | [sprint-19-backend-project-validation.md](sprints/sprint-19-backend-project-validation.md) | POST /api/validate/project — full workspace validation | `[x] done` |
| 20 | [sprint-20-backend-remove-obsolete.md](sprints/sprint-20-backend-remove-obsolete.md) | Remove Sections/Partial modes, BueloDsl, SectionsTemplateParser, ZIP bundle endpoints | `[x] done` |
| 21 | [sprint-21-backend-workspace-filesystem.md](sprints/sprint-21-backend-workspace-filesystem.md) | Workspace filesystem APIs (folders/files), dataSourcePath binding, import resolver | `[x] done` |

### 🚀 Sprints Ativas (QuestPDF C# era)

| Sprint | File | Goal | Status |
|--------|------|------|--------|
| B1 | [sprint-1-backend-core-engine.md](sprints/sprint-1-backend-core-engine.md) | TemplateEngine com C# IDocument; Roslyn compilation; validação; PageSettings | `[ ] pending` |
| B2 | [sprint-2-backend-api-mockdata.md](sprints/sprint-2-backend-api-mockdata.md) | ReportController endpoints completos; mock data flow; render pipeline | `[ ] pending` |
| B3 | [sprint-3-backend-global-artefacts.md](sprints/sprint-3-backend-global-artefacts.md) | GlobalArtefactStore como data sources JSON; data binding em templates | `[ ] pending` |
| B4 | [sprint-4-backend-multi-format.md](sprints/sprint-4-backend-multi-format.md) | Multi-format output (PDF + Excel); OutputRendererRegistry; performance | `[ ] pending` |

## Dependency Chain

```
[Sprints 6–21 arquivados/concluídos — DSL era]
    ↓
Sprint B1 (Core Rendering Engine — C# IDocument + Roslyn)
    ↓
Sprint B2 (Report API + Mock Data)
    ↓
Sprint B3 (Global Artefacts + Data Sources)
    ↓
Sprint B4 (Multi-Format Output)
    ↓
[Frontend sprints F1–F4 — see BueloWeb/ai/TASKS.md]
```

## Project Structure (reference)

```
Buelo.Contracts/        ← shared interfaces, models, enums (no business logic)
  PageSettings.cs       ← page size, margins, colors, watermark, font
  ReportRequest.cs      ← Template (C#), Data, PageSettings
  TemplateRecord.cs     ← stored template with MockData + PageSettings
  TemplateMode.cs       ← FullClass only (IDocument)
Buelo.Engine/           ← C# compilation, PDF generation, template store implementations
  TemplateEngine.cs     ← core: compile C#, bind data, render via QuestPDF
  Renderers/
    PdfRenderer.cs      ← QuestPDF
    ExcelRenderer.cs    ← ClosedXML
  Validators/
    CsharpFileValidator.cs
    JsonFileValidator.cs
Buelo.Api/              ← ASP.NET Core controllers and startup
  Controllers/
    ReportController.cs          ← /validate, /render
    TemplatesController.cs       ← CRUD templates
    GlobalArtefactsController.cs ← data sources
  Program.cs
Buelo.Tests/
  Engine/               ← unit tests for engine components
  Api/                  ← controller-level tests
```

## Conventions

- Each sprint modifies **Contracts → Engine → Api** in that order (dependency direction)
- Unit tests live in `Buelo.Tests/` alongside the layer being tested
- Templates are C# classes implementing `QuestPDF.Infrastructure.IDocument`
- No BueloDsl references — all DSL code has been removed
- After completing any sprint task, run `dotnet build` and `dotnet test` before marking done
