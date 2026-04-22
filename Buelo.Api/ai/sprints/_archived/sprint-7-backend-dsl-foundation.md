# Sprint 7 — Backend: DSL Foundation (Deprecate FullClass/Builder, Header Directives)

## Goal
Establish the Buelo Report DSL as the canonical authoring model. Deprecate `FullClass` and `Builder` modes. Add support for `@data`, `@settings`, and `@schema` header directives parsed from the template source before compilation.

## Status
`[x] done`

## Dependencies
- Sprint 6 complete ✅
- No new NuGet packages required

---

## Backend Scope (Buelo.Engine / Buelo.Contracts)

### 7.1 — Deprecate `FullClass` and `Builder` in `TemplateMode`

File: `Buelo.Contracts/TemplateMode.cs`

- Mark `FullClass` and `Builder` with `[Obsolete("Use Sections or Partial instead. FullClass and Builder will be removed in v2.")]`
- Do NOT remove them yet — keep runtime support for backward compatibility

### 7.2 — Create `TemplateHeader` model

File: `Buelo.Contracts/TemplateHeader.cs`

New record that represents parsed directives from the top of a template:

```csharp
public record TemplateHeader
{
    public string? DataRef { get; init; }        // @data from "file-or-id"
    public TemplateHeaderSettings? Settings { get; init; } // @settings { ... }
    public string? SchemaInline { get; init; }   // @schema record TypeName(...)
    public IReadOnlyList<string> ImportRefs { get; init; } = [];
    public IReadOnlyList<TemplateHeaderHelper> Helpers { get; init; } = [];
}

public record TemplateHeaderSettings
{
    public string? Size { get; init; }    // e.g. "A4", "A3", "Letter"
    public string? Margin { get; init; }  // e.g. "2cm", "1in"
    public string? Orientation { get; init; } // "Portrait" | "Landscape"
}

public record TemplateHeaderHelper(string Name, string Signature, string Body);
```

### 7.3 — Create `TemplateHeaderParser`

File: `Buelo.Engine/TemplateHeaderParser.cs`

Static class with method:

```csharp
public static class TemplateHeaderParser
{
    public static (TemplateHeader header, string strippedSource) Parse(string source);
}
```

Rules:
- Scans lines from top until a non-directive line is encountered
- Recognized directives (case-sensitive `@` prefix):
  - `@data from "<ref>"` → sets `DataRef`
  - `@settings { size: A4; margin: 2cm; orientation: Portrait; }` → sets `Settings`
  - `@schema record Name(...);\n` → sets `SchemaInline` (captures until `;`)
  - `@import <alias> from "<ref>"` → appends to `ImportRefs` (already handled by `SectionsTemplateParser`, now also extracted in header)
  - `@helper Name(params) => expr;` → appends to `Helpers`
- Returns the header model + source with directive lines removed
- Does not throw on unrecognized directives — logs and ignores

### 7.4 — Integrate `TemplateHeaderParser` into `TemplateEngine`

File: `Buelo.Engine/TemplateEngine.cs`

In `RenderAsync`:
1. If mode is `Sections`, call `TemplateHeaderParser.Parse(source)` before passing to `SectionsTemplateParser`
2. Expose parsed `TemplateHeader` as part of the render pipeline (store in local var for now, will be used in Sprint 8 for `@data` auto-injection)
3. Map `Settings` to `PageSettings` overrides (pass into `ReportContext.Globals` under key `"__pageSettings"`)

### 7.5 — Update `SectionsTemplateParser` to work with stripped source

File: `Buelo.Engine/SectionsTemplateParser.cs`

- `SectionsTemplateParser.Parse` should receive the already-stripped source (no directives)
- No change to its block parsing logic

### 7.6 — Add validation endpoint

File: `Buelo.Api/Controllers/ReportController.cs`

New endpoint:

```
POST /api/report/validate
```

Body: `{ "template": "...", "mode": "Sections" }`

Response on success: `200 { "valid": true }`  
Response on error: `200 { "valid": false, "errors": [{ "message": "...", "line": 3, "column": 12 }] }`

- Uses `TemplateEngine` compile path but skips PDF generation
- Maps Roslyn diagnostics back to user source line numbers (subtract wrapper lines)
- Always returns `200` — the `valid` bool signals success/failure

### 7.7 — Unit Tests

File: `Buelo.Tests/Engine/TemplateHeaderParserTests.cs`

Cover:
- Empty source returns empty header
- `@data` directive extracted correctly
- `@settings` parsed to `TemplateHeaderSettings`
- Mixed directives + C# body: stripped source starts at first non-directive line
- Unrecognized directive is ignored without throwing
- `@import` in header duplicates into `ImportRefs` (same as `SectionsTemplateParser` picks up)

---

## Acceptance Criteria
- [x] `FullClass` and `Builder` compile with `[Obsolete]` warning, still functional
- [x] `TemplateHeaderParser.Parse` passes all unit tests
- [x] `POST /api/report/validate` returns structured error list for invalid Sections templates
- [x] Existing `Sections` templates render identically after the stripped-source refactor
- [x] No breaking changes to existing API contracts
