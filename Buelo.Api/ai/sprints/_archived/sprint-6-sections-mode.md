# Sprint: Sections Mode — Fluent Declarative Template Syntax

## Motivation

The current `Builder` mode still requires the author to write a complete
`Document.Create(container => { container.Page(page => { ... }); }).GeneratePdf();`
expression.  For the majority of reports this is repetitive boilerplate that
obscures the meaningful parts: page configuration, header, content, and footer.

This sprint introduces **`TemplateMode.Sections`** — a new mode where the author
only declares the four semantic blocks of a page, and the engine assembles the
`Document.Create` scaffolding automatically.

---

## New Syntax (`TemplateMode.Sections`)

A Sections template is composed of up to four vertical blocks, in any order.
All blocks except `page.Content()` are optional.

### 1 — Page Configuration Block

A lambda assigned to the implicit `page` variable.  The block sets up size,
margins, background color, and default text style.

```csharp
page => {
    page.Size(PageSizes.A4);
    page.Margin(2, Unit.Centimetre);
    page.PageColor(Colors.White);
    page.DefaultTextStyle(x => x.FontSize(12));
}
```

> When this block is omitted, the engine falls back to the
> `ReportContext.PageSettings` values (already available on the context).

### 2 — Header Block

```csharp
page.Header()
    .Text((string)data.name)
    .SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);
```

### 3 — Content Block (required)

```csharp
page.Content()
    .PaddingVertical(1, Unit.Centimetre)
    .Column(x => {
        x.Spacing(20);
        x.Item().Text(Placeholders.LoremIpsum());
        x.Item().Image(Placeholders.Image(100, 100));
    });
```

### 4 — Footer Block

```csharp
page.Footer()
    .AlignCenter()
    .Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
```

### Import Directives

Shared header/footer fragments stored as `TemplateMode.Partial` records can be
injected with `@import`:

```csharp
@import header from "shared-header"        // resolve by Name
@import footer from "3fa85f64-…"           // resolve by Guid

page => {
    page.Size(PageSizes.A4);
    page.Margin(2, Unit.Centimetre);
}

page.Content()
    .PaddingVertical(1, Unit.Centimetre)
    .Column(x => {
        x.Item().Text((string)data.title);
    });
```

When `@import header` is present the inline `page.Header()` block (if any) is
**ignored** in favour of the imported fragment.  Same rule applies for footer.

---

## New Mode: `TemplateMode.Partial`

A template that provides **one or more named sections** to be imported into a
Sections-mode template.  A Partial record's `Template` field contains only the
section body — the chain that follows `page.Header()`, `page.Footer()`, etc.

```csharp
// Example Partial for a standard company header
.Height(50)
.Row(row => {
    row.RelativeItem().Text("Acme Corp").Bold().FontSize(18);
    row.ConstantItem(100).AlignRight().Text((string)data.reportDate);
});
```

> **The `@import` directive indicates _which slot_ (header | footer | content)
> the fragment fills.**  The Partial record itself does not declare the slot;
> the host template does.

---

## Generated Scaffolding (internal)

The engine's `WrapSectionsTemplate` method will produce a class equivalent to:

```csharp
public class Report : IReport
{
    public byte[] GenerateReport(ReportContext ctx)
    {
        var data    = ctx.Data;
        var helpers = ctx.Helpers;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // ── page config block (from template or PageSettings fallback) ──
                <PAGE_CONFIG_BLOCK>

                // ── header ──
                page.Header()
                    <HEADER_BODY>;

                // ── content ──
                page.Content()
                    <CONTENT_BODY>;

                // ── footer ──
                page.Footer()
                    <FOOTER_BODY>;
            });
        }).GeneratePdf();
    }
}
```

---

## Technical Design

### Contracts (`Buelo.Contracts`)

| Change | Detail |
|---|---|
| Add `TemplateMode.Sections` | New enum value; auto-detected by the engine heuristic |
| Add `TemplateMode.Partial` | Marks templates intended as reusable fragments |

### Engine (`Buelo.Engine`)

| Change | Detail |
|---|---|
| `SectionsTemplateParser` (new class) | Parses `@import` directives, the page config block, and each named `page.*()` section out of raw source |
| `TemplateEngine` — constructor | Receive `ITemplateStore` (alongside existing `IHelperRegistry`) to resolve `@import` targets at wrapping time |
| `TemplateEngine.WrapSectionsTemplate()` | Counterpart to the existing `WrapBuilderTemplate()`, assembles the final compilable code string |
| `TemplateEngine.ResolveTemplateMode()` heuristic | Extend to detect Sections mode when source starts with `@import` or with `page =>` (not inside a lambda) |
| Compilation cache | The SHA-256 cache already in place handles the wrapped code transparently |

### API (`Buelo.Api`)

No new endpoints.  Existing `POST /api/report/render` and template CRUD endpoints
already accept `Mode` on the request/record, so `Sections` and `Partial` flow
through automatically.

---

## Implementation Tasks

### Milestone 1 — Contracts & Enum

- [x] **T-01** Add `TemplateMode.Sections` to `TemplateMode.cs` with XML doc comment
- [x] **T-02** Add `TemplateMode.Partial` to `TemplateMode.cs` with XML doc comment
- [x] **T-03** Update `buelo-system.instructions.md` — add table rows for the two new modes

### Milestone 2 — Parser

- [x] **T-04** Create `Buelo.Engine/SectionsTemplateParser.cs`
  - `ParseImports(string source)` → `IReadOnlyList<ImportDirective>` (`Slot`, `NameOrId`)
  - `ParsePageConfig(string source)` → `string?` (lambda body or null)
  - `ParseSection(string source, string slot)` → `string?` (fluent chain after `page.Header()`, etc.)
  - `StripDirectives(string source)` → source without `@import` lines
- [x] **T-05** Define `ImportDirective` record: `Slot` (enum: `Header | Footer | Content`), `Target` (string)

### Milestone 3 — Engine Integration

- [x] **T-06** Inject `ITemplateStore` into `TemplateEngine` constructor; update `EngineExtensions` accordingly
- [x] **T-07** Implement `TemplateEngine.WrapSectionsTemplate(string source, IReadOnlyList<SectionFragment> imports)` 
  - Resolves each `@import` target from the store (by Guid first, then by Name)
  - Substitutes import bodies into the correct slot
  - Falls back to inline blocks when no import is found for a slot
- [x] **T-08** Update `TemplateEngine.ResolveTemplateMode()` to detect Sections mode heuristics:
  - First non-whitespace line starts with `@import`
  - OR `page =>` appears outside a nested lambda (simple `TrimStart().StartsWith("page =>")` check)
- [x] **T-09** Wire `WrapSectionsTemplate` into `RenderAsync` alongside `WrapBuilderTemplate`

### Milestone 4 — Tests

- [x] **T-10** `Buelo.Tests/Engine/SectionsTemplateParserTests.cs`
  - Parse `@import` lines (valid, invalid, mixed with code)
  - Parse page config block (present / absent)
  - Parse each section (header / content / footer present / absent)
- [x] **T-11** `Buelo.Tests/Engine/TemplateEngineTests.cs` — add Sections-mode cases
  - Render without imports (content-only, all four sections)
  - Render with `@import header` from a stored Partial record
  - Render with `@import footer` from a stored Partial record
  - Verify inline block is replaced when the same slot is imported
- [x] **T-12** `Buelo.Tests/Engine/TemplateModeDetectionTests.cs`
  - Heuristic correctly identifies Sections, Builder, FullClass sources
- [x] **T-13** `Buelo.Tests/Api/TemplatesControllerTests.cs` — exercise `POST /api/templates` with `Mode = Sections`

### Milestone 5 — Docs & Cleanup

- [x] **T-14** Update `README.md` — add Sections mode to the "Template Modes" section with a full example
- [x] **T-15** Update `buelo-system.instructions.md` — document `SectionsTemplateParser` and import directive syntax
- [x] **T-16** Add a `Sections` example in `PAGE_SETTINGS_GUIDE.md` showing how `PageSettings` interacts with the page config block

---

## Acceptance Criteria

1. A `POST /api/report/render` request with `Mode: "Sections"` and a body
   containing only a `page.Content()` block renders a valid PDF.
2. A Sections template with all four blocks (`page =>`, `page.Header()`,
   `page.Content()`, `page.Footer()`) renders identically to a handwritten
   `Builder` template producing the same document.
3. `@import header from "<id>"` resolves a stored `Partial` record and its body
   is injected as the header; a local `page.Header()` block in the same template
   is ignored when the import succeeds.
4. `@import header from "<name>"` resolves by `TemplateRecord.Name` when the
   target is not a valid Guid.
5. If an `@import` target cannot be found in the store, the engine falls back to
   the inline block for that slot (no exception).
6. All existing `Builder` and `FullClass` tests continue to pass (no regression).
7. The SHA-256 compilation cache is shared — a Sections template compiled once
   is not recompiled on the next identical request.

---

## Open Questions

| # | Question | Owner | Status |
|---|---|---|---|
| OQ-1 | Should `TemplateMode.Partial` records be renderable standalone (e.g., for preview)? | BE | Open |
| OQ-2 | Should import resolution be eager (at wrap time) or lazy (at render time)? Lazy allows hot-swapping shared fragments without restarting the app but breaks the cache strategy. | BE | Open |
| OQ-3 | Should the page config block in a Sections template **override** or **merge with** `ReportContext.PageSettings`? | BE | Open |
| OQ-4 | Maximum import depth — should a Partial be allowed to `@import` itself? | BE | Open |
