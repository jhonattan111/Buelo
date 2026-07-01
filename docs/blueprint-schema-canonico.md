# Blueprint — Canonical Schema of Declarative Buelo

> **Historical design document** (no implementation code) — kept for the original rationale, not as
> current reference. **The declarative engine described here is implemented and live.** For the
> maintained, up-to-date spec (blocks, expressions, modules, examples), see
> [`BueloApi/docs/reference/`](../BueloApi/docs/reference/). Component (§7) and validator (§8) design
> have migrated there (`reference/modules.md`) and were removed from this file to avoid drift between
> two copies.
>
> Defines Buelo's declarative model, the `BueloDocument` IR it produces, and how everything fits into
> the **engine × recipe × extension** architecture (inspired by jsreport, with QuestPDF as the recipe).
>
> Status: drafted 2026-06-26, **implemented** since. Scope: declarative engine (the C# engine is parallel, see §10).

---

## 1. Framing

Buelo separates three orthogonal responsibilities (jsreport model):

- **Engine** — transforms *authoring language + data* into a document. Buelo has two: the **declarative interpreter** (YAML/JSON → `BueloDocument`) and the **C# compiler** (Roslyn → `IDocument`).
- **Recipe** — transforms the document into bytes. Already exists: `OutputRendererRegistry` → `PdfRenderer` (QuestPDF), `ExcelRenderer` (ClosedXML).
- **Extension** — registrable plugins (helpers, validators, new recipes). Self-hosted ⇒ custom code is trusted.

This blueprint specifies the **declarative engine** and the **IR** it emits.

### Pipeline

```
.yml files (report + imports)
  → parse + resolve imports/symbols
  → evaluate expressions and directives (forEach/if/groupBy) against the data
  → BueloDocument (typed IR, no pending expressions)
  → recipe (QuestPDF / ClosedXML)
  → bytes (PDF / XLSX)
```

**Principle:** the `BueloDocument` is the contract between engine and recipe. Any engine that produces a valid `BueloDocument` renders on any recipe. Expressions and directives do **not** reach the recipe — they are resolved beforehand.

---

## 2. File model (`kind`)

Every file declares a `kind` at the top. A product = a set of these files (in the local template and/or in the global artifact registry).

| `kind` | Role | Renderable? |
|---|---|---|
| `report` | A complete report | ✅ |
| `component` | Reusable layout fragment (params + slots) | via `use` |
| `styles` | Named style classes | importable |
| `formats` | Named masks/formats | importable |
| `lib` | Named pure expressions (calculated fields) | importable |
| `validator` | Declarative validation rules | importable |
| `theme` | Bundle of `styles` + `page settings` + palette | importable |

**Name resolution:** **template-local** artifact first, then **global registry**. Collision → compilation error with the origin of each definition.

**Versioning:** `use: defaultLayout@2` (optional pin). No pin = latest version.

---

## 3. Anatomy of a `report`

```yaml
kind: report
name: invoice

meta:
  engine: declarative          # declarative | csharp
  recipe: pdf                  # pdf | excel
  page: { size: A4, margin: 2cm, orientation: portrait }

import:                        # libraries this report uses
  - styles: corporate
  - formats: br
  - component: defaultLayout

data:
  source: invoice.json         # data binding (file, or injected via API)

use: defaultLayout             # optional: wraps the report in a layout component
with:
  reportName: "Invoice #{{ data.number }}"

content:                       # body blocks (fill the defaultLayout slot)
  - text: { value: "Items", class: title }
  - table: { ... }             # see §5
  - if: "{{ data.comments }}"
    card: { class: note }
    content:
      - markdown: "{{ data.comments }}"
```

`meta` carries what today lives in `TemplateRecord` (engine, recipe/`OutputFormat`, `PageSettings`). `use`/`with` is the default-layout mechanism (§7).

---

## 4. Layout vocabulary (blocks)

Each block maps to an IR node and, from there, to a QuestPDF call. Core list (v1):

| Block | Description | QuestPDF (recipe) |
|---|---|---|
| `text` | Plain text or with runs/spans | `.Text(...)` |
| `markdown` / `html` | Rich content (subset → runs) | spans/elements |
| `row` | Items side by side | `.Row(...)` |
| `column` | Stacked items | `.Column(...)` |
| `table` | Data-oriented table | `.Table(...)` |
| `image` | Image (url/base64/artifact) | `.Image(...)` |
| `card` / `panel` | Container with border/background/padding | `.Background().Border().Padding()` |
| `chart` | Curated chart (bar/line/pie) | composition of primitives |
| `spacer` | Vertical space | `.PaddingVertical(...)` |
| `line` / `divider` | Rule/separator | `.LineHorizontal(...)` |
| `pageBreak` | Page break | `.PageBreak()` |

### Bands (page structure)

`page` has three slots, in the banded spirit (Stimulsoft/jsreport):

```yaml
page:
  header:  [ ... ]    # repeats at the top of each page
  content: [ ... ]    # main flow
  footer:  [ ... ]    # repeats at the bottom (page/pageCount available)
```

### Sizing

`width`/`height` accept: `*` (relative, weight 1), `3*` (weight 3), `120px`, `40%`, `2cm`. They map to `RelativeColumn`/`ConstantColumn`/etc.

### Style

Three forms, combinable (precedence: inline > class > theme):

```yaml
text:
  value: "Report"
  class: title                               # from a `kind: styles`
  style: { color: "#c00", bold: true }       # inline override
```

---

## 5. Table (data-oriented)

```yaml
table:
  data: data.items                # array to iterate
  rowStyle: { borderBottom: "1px #DDD", paddingY: 5 }
  columns:
    - { width: 25px, header: "#",        cell: "{{ index + 1 }}" }
    - { width: 3*,   header: "Product",  cell: "{{ item.name }}" }
    - { width: 1*,   header: "Unit",     cell: "{{ item.price | currency }}", class: monetary }
    - { width: 1*,   header: "Qty",      cell: "{{ item.qty }}", align: right }
    - { width: 1*,   header: "Total",    cell: "{{ currency(item.price * item.qty) }}", class: monetary }
  footer:
    - { span: 4, text: "Total", style: { bold: true, align: right } }
    - { text: "{{ currency(sum(data.items, 'price * qty')) }}", class: monetary }
```

`cell` is an expression evaluated per row; `item`, `index`, `first`, `last` are row context variables.

### Grouping

```yaml
table:
  data: data.employees
  groupBy: department
  group:
    header: { text: "{{ group.key }}", style: { bold: true, background: "#E8E8E8" } }
    footer: { text: "Subtotal: {{ currency(sum(group.items, 'salary')) }}", class: monetary }
  columns: [ ... ]
```

---

## 6. Expression language `{{ }}` (deliberately simple)

**Principle (the anti-inner-platform line):** expressions are **pure, single-line, deterministic, side-effect-free**. There is no imperative `for`/`if`, function definition, or state. Flow control lives in **directives** (`forEach`/`if`/`groupBy`). Real algorithms live in a **C# extension** (self-hosted ⇒ cheap and safe). This boundary is what keeps the YAML from becoming a bad language.

### Data access
`data.field`, `data.list[0].x`, and in the row/group scope: `item`, `index`, `first`, `last`, `group.key`, `group.items`.

### Context variables
`now`, `today`, `page`, `pageCount`, `report.name`.

### Operators
arithmetic `+ - * / %`, comparison `== != < <= > >=`, logical `&& || !`, ternary `cond ? a : b`, null-coalescing `??`.

### Standard library (engine stdlib — extends `IHelperRegistry`)
- **Aggregation:** `sum(lista, 'expr')`, `avg`, `count`, `min`, `max`
- **Format (BR by default):** `currency(x)`, `date(x, 'dd/MM/yyyy')`, `cnpj(x)`, `cpf(x)`, `cep(x)`, `phone(x)`, `percent(x)`
- **String:** `upper`, `lower`, `trim`, `join(lista, sep)`, `len`, `mask(x, '##.###')`, `digits(x)`
- **Logic:** `if(cond, a, b)`, `coalesce(...)`

### Pipes (sugar)
`{{ data.cnpj | cnpj }}` ≡ `{{ cnpj(data.cnpj) }}`; chainable: `{{ data.valor | currency }}`.

### Reusable named expressions (`kind: lib`)
```yaml
kind: lib
name: sales
expr:
  finalPrice: "{{ price * (1 - discount) }}"
  marginPct:  "{{ (revenue - cost) / revenue * 100 }}"
```
Usage: `{{ sales.finalPrice }}` (with `item`/`data` in scope).

---

## 7. Components (params + slots) & 8. Validators

Design superseded by the implementation — see
[`BueloApi/docs/reference/modules.md`](../BueloApi/docs/reference/modules.md) for the current
`kind: component` (params/slots/body) and `kind: validator` (3-tier: declarative/expression/C#
reference) shapes, which follow the composition-over-inheritance and escalating-tiers approach
outlined here.

---

## 9. The IR — `BueloDocument`

The *lowering* target. Typed model, **with no pending expressions** (everything already evaluated), **no imports** (everything already resolved), **no styles by name** (everything already flattened). It's what the recipe consumes.

Node taxonomy (sketch):

```
BueloDocument
  Meta { page, recipe, fonts }
  Page { header: Node[], content: Node[], footer: Node[] }

Node (abstract) — all carry a resolved Style:
  TextNode      { runs: Run[] }                 Run { text, style }
  RowNode       { items: { size, node }[] }
  ColumnNode    { items: Node[], spacing }
  TableNode     { columns: Col[], rows: Cell[][], groups?: Group[], footer?: Cell[] }
  ImageNode     { source, fit }
  ContainerNode { kind: card|panel, children: Node[] }   // border/background/padding in Style
  ChartNode     { type, series, axis }
  ChartNode/LineNode/SpacerNode/PageBreakNode ...

Style (resolvido) { font, size, bold, italic, color, bg, align, padding, margin, border, width, height }
```

### Construct → IR → QuestPDF mapping (proof that it lowers)

| Declarative | IR Node | QuestPDF |
|---|---|---|
| `page.header` | `Page.header` | `page.Header()` |
| `text` / `markdown` | `TextNode` (runs) | `.Text(t => t.Span(...))` |
| `row` | `RowNode` | `.Row(r => r.RelativeItem()/.ConstantItem())` |
| `column` | `ColumnNode` | `.Column(c => c.Item())` |
| `table` (+forEach/groupBy resolved) | `TableNode` | `.Table(...)` + `.Header()` |
| `card` | `ContainerNode` | `.Background().Border().Padding()` |
| `image` | `ImageNode` | `.Image(...)` |
| `{{ page }}` | already in a dynamic `Run` | `CurrentPageNumber()` |

> The same IR feeds the Excel recipe (ClosedXML): `TableNode` becomes a worksheet, `TextNode` becomes a cell. Recipes that don't support a node (e.g. `chart` in Excel) degrade in a defined way.

---

## 10. Relationship with the C# engine (summary)

Out of scope for this blueprint, but to situate it:

- **C# "raw IDocument"** — full QuestPDF power, goes straight to the PDF recipe (doesn't pass through the IR). It's the escape hatch.
- **Eject** — generates, from a `BueloDocument`, an equivalent C# `IDocument`. Graduation path from declarative → code.
- The IR is the convergence point of the **declarative** path; raw-C# converges on QuestPDF itself. An accepted, conscious trade-off.

---

## 11. JSON Schema → IntelliSense for free

Each `kind` gets a **JSON Schema**. On the frontend, `monaco-yaml` + that schema provide autocomplete, inline validation, and hover **without a language server** — solving objective #1 (fluid editor) for the declarative surface, which is where it's cheap to deliver.

---

## 12. Open decisions (need you)

> **Status 2026-06-28:** almost everything here has been **decided and implemented**. (1) YAML only ✅ · (2) markdown
> leads ✅ (`markdown` block; `html`-subset not done) · (3) stdlib v1 delivered ✅ · (4) **optional** pin
> (advisory — `StripPin` removes `@version`, doesn't validate) · (5) component scope = params + minimal
> ambient ✅ · (6) chart = v2 (out). Only loose ends remain: `validate:` on a column during render, the IR's
> Excel recipe, and the `html`-subset — see handoff.

1. **Canonical serialization:** ✅ **decided (2026-06-28) — YAML only** for definitions (authoring). The **data** that feeds the report stays **JSON** (comes from the API). A single parser in the vertical slice: YamlDotNet. JSON-as-definition may come later if the API needs it, but it's not v1.
2. **Rich content:** `markdown` leads, `html`-subset secondary. To confirm.
3. **Richness of stdlib v1:** which functions go in now (suggestion: aggregation + BR formats + basic string). Define the cut list.
4. **Version pin:** default-to-latest vs mandatory pin in production. I recommend optional pin.
5. **Component scope:** explicit params + minimal ambient (recommended) vs ambient access to `data`.
6. **Chart:** ✅ **decided — v2.** Stays out of the vertical slice; comes after the basic reports are working.

---

## 13. Persistence (decided 2026-06-26, fork resolved 2026-06-28) — implemented

> **Implemented 2026-06-30** as designed below: `Buelo.Persistence` (EF Core, SQLite default) +
> `Buelo.Persistence.Postgres`. See [`BueloApi/CLAUDE.md`](../BueloApi/CLAUDE.md)'s "Declarative engine"
> section for the current persistence model and config keys.

**Decision: everything pluggable, but along TWO different AXES** — because definitions and operational data have distinct paradigms.

| Domain | Abstraction | Dev default | Prod default | Other providers |
|---|---|---|---|---|
| **Definitions** | `ITemplateStore` (custom, **already exists** in the code) | **fs (git)** | fs (git) *or* Postgres | InMemory (tests), SQLite |
| **Operational** | **EF Core `DbContext`** (provider via config) | **SQLite** | **PostgreSQL** | InMemory (tests) |

> **Why different axes?** Definitions have a backend (fs/git) that **is not a database** — it gives diff/PR/review for free, something no DB delivers. That's why they need their own interface (`ITemplateStore`, which already exists with InMemory + FileSystem impls). Operational is all relational ⇒ **EF Core itself is already the abstraction**: swap `UseSqlite()`↔`UseNpgsql()` via config, **a single entity model**, no extra interface.

### Definitions — pluggable `ITemplateStore` (fork resolved)

Provider chosen via config. **Default fs-store** (`.yml` files on disk): git versioning for free, readable, jsreport model, reuses `FileSystemWorkspaceStore`/`FileSystemTemplateStore` which **already exist**. SQLite/Postgres providers (definition as jsonb/text blob + metadata) are available through the same interface for anyone who prefers a single store. No irreversible decision: the abstraction is already in the code.

### Operational — EF Core, provider via config

**SQLite default in dev** (file or `:memory:`, **zero infra** — starts the API without a container), **PostgreSQL default in prod** (write concurrency under load + multi-node with a shared database). Same entity model in both.

- Postgres's gain is write concurrency and horizontal scale — **not** volume (SQLite handles a lot on a single instance).
- **Provider-agnostic schema:** normal columns; JSON field (e.g. `parameters`) stored as text/jsonb via a value-converter that works on both providers.
- **Caveat — migrations are per-provider:** you keep one set of migrations for SQLite and another for Postgres (or `EnsureCreated` in dev SQLite). A small, known cost.

### Profiles of the two domains

- **Definitions** (`report`, `component`, `styles`, `formats`, `lib`, `validator`, `theme`): document, versioned, read-heavy, few records.
- **Operational data** (issuance logs, login/audit, render history, parameters): append-heavy, queried/aggregated.

### Instance config ≠ definition

Database connection string, API keys, and auth settings live in **env/secrets** (chicken-and-egg: they can't live in the database they initialize). A "report configuration file" (theme/page/parameters) **is** a definition and follows the rule above.

### Implementation sequence
**Definitions don't block the vertical slice** — the abstraction (`ITemplateStore` / `FileSystemWorkspaceStore`) already exists. Operational persistence (EF Core) is hardening and comes later.

1. **Declarative vertical slice first** (doesn't depend on DbContext): YAML → interpreter → `BueloDocument` → QuestPDF recipe → PDF, reading the definition from the fs-store that already exists.
2. **Operational later (hardening):** EF `DbContext` + operational entities → SQLite default (dev, zero infra) → Postgres provider (prod) → logs/auth/audit/render-history tables.
