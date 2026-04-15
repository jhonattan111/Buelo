# SKILLS.md — Buelo Backend

## Development Workflow

After completing any sprint task:

```powershell
dotnet build
dotnet test
```

Both must pass with zero errors before marking a task done.

## Architecture Rules

| Rule | Detail |
|------|--------|
| **Dependency direction** | `Buelo.Api` → `Buelo.Engine` → `Buelo.Contracts` — never reverse |
| **Contracts only** | `Buelo.Contracts` holds interfaces, records, and enums. No business logic, no QuestPDF, no Roslyn |
| **Async all the way** | All `ITemplateStore` methods are `Task<T>` even if the implementation is synchronous |
| **Primary constructors** | Controllers use C# 12 primary constructor injection |
| **New GUID on first save** | `Id == Guid.Empty` signals new record — the store assigns the GUID, not the controller |

## Key Types (Buelo.Contracts)

| Type | Purpose |
|------|---------|
| `IReport` | `byte[] GenerateReport(ReportContext ctx)` — every compiled template implements this |
| `ITemplateStore` | Persistence abstraction; swap implementations without engine changes |
| `IHelperRegistry` | Formatting helpers available as `ctx.Helpers` / `helpers` inside templates |
| `ReportContext` | `{ Data: dynamic, Helpers, Globals }` — passed into every render |
| `TemplateRecord` | Persisted entity with `Template` (C# source), `Mode`, `Artefacts`, etc. |
| `TemplateMode` | `Sections` (preferred), `Partial`, `FullClass` (deprecated), `Builder` (deprecated) |

## Engine Pipeline (TemplateEngine.RenderAsync)

```
source
  → TemplateHeaderParser.Parse()          ← Sprint 7: strips @directives, returns TemplateHeader
  → SectionsTemplateParser.Parse()        ← processes section blocks, resolves @import
  → WrapSectionsTemplateAsync()           ← builds compilable C# class
  → SHA-256 hash → cache lookup
  → CSharpScript.EvaluateAsync<IReport>() ← Roslyn compile (on cache miss)
  → data → ExpandoObject
  → IReport.GenerateReport(context)       ← returns PDF bytes
```

## Template Modes

| Mode | Status | Description |
|------|--------|-------------|
| `Sections` | ✅ Preferred | DSL blocks: `page =>`, `page.Header()`, `page.Content()`, `page.Footer()` |
| `Partial` | ✅ Preferred | Reusable fragment imported by Sections via `@import` |
| `FullClass` | ⚠️ Deprecated | Full `IReport` class — keep runtime support, mark `[Obsolete]` |
| `Builder` | ⚠️ Deprecated | Return expression only — keep runtime support, mark `[Obsolete]` |

## DSL Directives (Sprint 7+)

Directives appear at the top of a `Sections` template and are stripped before compilation:

| Directive | Syntax | Effect |
|-----------|--------|--------|
| `@import` | `@import alias from "name-or-guid"` | Imports a Partial template |
| `@data` | `@data from "artefact-name"` | Binds a data artefact for render |
| `@settings` | `@settings { size: A4; margin: 2cm; }` | Page size/margin/orientation |
| `@schema` | `@schema record Name(string Prop);` | Inline typed record for data binding |
| `@helper` | `@helper Name(params) => expr;` | Inline helper function |
| `@helper from` | `@helper from "artefact-name"` | Load helpers from `.helpers.cs` artefact |

## Adding a New Endpoint

1. Add route to the relevant controller (`ReportController` or `TemplatesController`)
2. If new request/response shapes are needed, add records to `Buelo.Contracts`
3. If new engine behavior is needed, add to `Buelo.Engine` and call from controller via injected `TemplateEngine`
4. Add at least one happy-path test in `Buelo.Tests/Api/`

## Common Pitfalls

- **Roslyn errors point to wrapped code** — when surfacing compile errors to callers, subtract the number of wrapper lines injected by `WrapSectionsTemplateAsync` to get the user's original line number
- **Hash cache** — the compiled `IReport` is cached by SHA-256 of the **final** wrapped code. Changing _only_ the wrapper (not the user source) invalidates cache correctly; changing only helper artefacts must also bust the cache
- **`[Obsolete]` is not removal** — deprecated modes must still compile and render correctly; only warn at the call site
- **`DeleteAsync` on FileSystemStore removes the entire folder** — always confirm before deleting; do not use `recursive: true` on a path derived from user input without validation
