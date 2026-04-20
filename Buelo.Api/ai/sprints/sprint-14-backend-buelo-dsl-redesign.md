# Sprint 14 — Backend: .buelo DSL Redesign (YAML-like Component Language)

## Goal
Replace the raw C# QuestPDF fluent API as the primary authoring model with a declarative, YAML-inspired `.buelo` component language. Reports become component trees that are parsed, validated, and compiled to QuestPDF at render time. The language is designed to be learnable without C# knowledge, extensible with new components, and suitable for high-quality IntelliSense support.

Backward compatibility: the existing C# Sections mode continues to work. `.buelo` mode is opt-in via the new `TemplateMode.BueloDsl` value.

## Status
`[ ] pending`

## Dependencies
- Sprint 13 complete ✅ (global artefact store + file extension conventions)

---

## Language Design Reference

### File extensions (canonical)

| File | Extension | Usage |
|------|-----------|-------|
| Report / Partial | `.buelo` | Main report or reusable partial |
| Helper scripts | `.csx` | C# helper functions (preferred) |
| Helper scripts | `.cs` | C# helper class (alternative) |
| Data files | `.json` | Data for binding or mock |
| Project config | `.bueloproject` | Workspace settings (Sprint 15) |

---

### .buelo syntax overview

A `.buelo` file is a YAML-like document composed of **directives** (at the top) and **component blocks** (below). Each component block is identified by its **component type keyword** followed by an optional **title**, then indented properties.

```yaml
# invoices.buelo — Example report

import { FormatCNPJ, FormatCurrency } from "formatters"
@data from "colaborador.json"
@settings
  size: A4
  orientation: Portrait
  margin: 2cm

report title:
  text: "Relatório de Colaboradores"
  style:
    fontSize: 18
    bold: true
    color: "#333333"
    align: center

page header:
  text: "Sistema de RH — {{ data.empresa }}"
  style:
    fontSize: 10
    color: "#666666"

page footer:
  text: "Página {{ page }} de {{ pageCount }}"
  style:
    align: center
    fontSize: 9

data:
  table:
    columns:
      - field: nome
        label: Nome Completo
        width: 40%
      - field: cargo
        label: Cargo
        width: 30%
      - field: salario
        label: Salário
        width: 30%
        format: currency

    group header:
      field: departamento
      text: "{{ value }}"
      style:
        bold: true
        backgroundColor: "#E8E8E8"
        padding: 4px

    group footer:
      text: "Subtotal: {{ FormatCurrency(subtotal) }}"
      style:
        align: right
        italic: true

report resume:
  panel:
    style:
      border: 1px solid "#CCCCCC"
      padding: 8px
    text: "Total de colaboradores: {{ count }}"
    text: "Gerado em: {{ FormatDate(now) }}"
```

---

### Import syntax

```
import { FunctionName } from "artefact-name"
import { FormatCNPJ, FormatCurrency } from "formatters"
import * from "formatters"
```

- Resolved against local template artefacts first, then global artefact store
- Functions become available in template expressions: `{{ FormatCNPJ(data.cnpj) }}`
- `import * from "name"` imports all exported functions

---

### Template expressions

Used inside `text:` values and `style:` color values:
- `{{ data.fieldName }}` — bind data field
- `{{ page }}` / `{{ pageCount }}` — page numbering
- `{{ FunctionName(arg) }}` — call imported helper function
- `{{ now }}` — current DateTime

---

### Layout components

| Component keyword | Description | Valid children |
|-------------------|-------------|----------------|
| `report title` | Top-of-report title block | content components |
| `report resume` | End-of-report summary block | content components |
| `page header` | Rendered at top of every page | content components |
| `page footer` | Rendered at bottom of every page | content components |
| `header` | Custom flexible header region | `header column` |
| `footer` | Custom flexible footer region | `footer column` |
| `header column` | Column within a `header` | content components |
| `footer column` | Column within a `footer` | content components |
| `group header` | Data group header (inside `data`) | content components |
| `group footer` | Data group footer (inside `data`) | content components |
| `data` | Data iteration container | `table`, content components |

### Content components

| Component keyword | Description | Properties |
|-------------------|-------------|------------|
| `text` | Plain or interpolated text | `style` |
| `image` | Embedded image | `src`, `width`, `height`, `style` |
| `rich text` | Multi-run formatted text | `runs[]` |
| `spacer` | Vertical whitespace | `height` |
| `panel` | Bordered/padded container | `style`, nested components |
| `card` | Elevated block with shadow | `style`, nested components |

### `table` properties

| Property | Type | Description |
|----------|------|-------------|
| `columns` | list | Column definitions |
| `columns[].field` | string | Data field name |
| `columns[].label` | string | Header label |
| `columns[].width` | string | e.g. `30%`, `120px`, `*` (fill) |
| `columns[].format` | string | `currency`, `date`, `percent`, or helper name |
| `group header` | block | Group header template |
| `group footer` | block | Group footer template |
| `zebra` | bool | Alternating row colors |
| `headerStyle` | style block | Column header style |

### `style` properties (universal)

| Property | Values | Description |
|----------|--------|-------------|
| `fontSize` | number | Font size in pt |
| `bold` | bool | Bold text |
| `italic` | bool | Italic text |
| `color` | hex / name | Text color |
| `backgroundColor` | hex / name | Background color |
| `align` | `left`, `center`, `right`, `justify` | Text alignment |
| `padding` | css-like (`4px`, `4px 8px`) | Inner padding |
| `margin` | css-like | Outer margin |
| `border` | css-like (`1px solid #CCC`) | Border |
| `width` | `%`, `px`, `*` | Element width |
| `height` | `px`, `cm`, `*` | Element height |
| `inherit` | string | Inherit style from named style definition |

---

## Backend Scope

### 14.1 — Add `BueloDsl` to `TemplateMode`

File: `Buelo.Contracts/TemplateMode.cs`

```csharp
public enum TemplateMode
{
    Sections = 0,
    Partial  = 1,
    BueloDsl = 2,   // YAML-like .buelo component language

    [Obsolete("Use Sections or Partial instead.")]
    FullClass = 10,
    [Obsolete("Use Sections instead.")]
    Builder   = 11,
}
```

Auto-detection rule: if source starts with `report title:`, `page header:`, `page footer:`, `data:`, `import {`, or `@settings` without `{` on the same line → treat as `BueloDsl`.

---

### 14.2 — AST node types

File: `Buelo.Engine/BueloDsl/BueloDslAst.cs`

```csharp
namespace Buelo.Engine.BueloDsl;

public record BueloDslDocument(
    BueloDslDirectives Directives,
    IReadOnlyList<BueloDslComponent> Components
);

public record BueloDslDirectives(
    IReadOnlyList<BueloDslImport> Imports,
    string? DataRef,
    BueloDslSettings? Settings
);

public record BueloDslImport(
    IReadOnlyList<string> Functions,  // empty = wildcard import
    string Source
);

public record BueloDslSettings(
    string? Size,
    string? Orientation,
    string? Margin
);

public abstract record BueloDslComponent(string ComponentType);

public record BueloDslLayoutComponent(
    string ComponentType,               // "report title", "page header", etc.
    BueloDslStyle? Style,
    IReadOnlyList<BueloDslComponent> Children
) : BueloDslComponent(ComponentType);

public record BueloDslTextComponent(
    string Value,                       // may contain {{ expressions }}
    BueloDslStyle? Style
) : BueloDslComponent("text");

public record BueloDslImageComponent(
    string Src,
    string? Width,
    string? Height,
    BueloDslStyle? Style
) : BueloDslComponent("image");

public record BueloDslTableComponent(
    IReadOnlyList<BueloDslTableColumn> Columns,
    BueloDslComponent? GroupHeader,
    BueloDslComponent? GroupFooter,
    bool Zebra,
    BueloDslStyle? HeaderStyle
) : BueloDslComponent("table");

public record BueloDslTableColumn(
    string Field,
    string Label,
    string? Width,
    string? Format
);

public record BueloDslStyle(
    int? FontSize,
    bool? Bold,
    bool? Italic,
    string? Color,
    string? BackgroundColor,
    string? Align,
    string? Padding,
    string? Margin,
    string? Border,
    string? Width,
    string? Height,
    string? Inherit
);
```

---

### 14.3 — `BueloDslParser`

File: `Buelo.Engine/BueloDsl/BueloDslParser.cs`

```csharp
public static class BueloDslParser
{
    public static BueloDslDocument Parse(string source);
    public static BueloDslDocument Parse(string source, out IReadOnlyList<BueloDslParseError> errors);
}

public record BueloDslParseError(string Message, int Line, int Column, BueloDslErrorSeverity Severity);

public enum BueloDslErrorSeverity { Error, Warning }
```

Parser rules:
1. **Directive block** (top of file, before first component keyword): parse `import`, `@data`, `@settings` lines
2. **Component blocks**: identified by non-indented lines matching component keyword patterns (see keyword table above)
3. **Properties**: indented key-value pairs under a component; `style:` introduces a nested property block
4. **Expressions**: `{{ ... }}` are preserved as-is in text values; validated that braces are balanced
5. **Comments**: `#` to end of line is stripped
6. **Errors**: unrecognized top-level keywords → `BueloDslErrorSeverity.Warning`; unbalanced expressions → `Error`

YAML parsing: use `YamlDotNet` (add NuGet) for inner property parsing; custom top-level scanner for component keyword detection.

---

### 14.4 — `BueloDslCompiler`

File: `Buelo.Engine/BueloDsl/BueloDslCompiler.cs`

Traverses `BueloDslDocument` AST and generates a C# Sections-mode source string:

```csharp
public static class BueloDslCompiler
{
    public static string Compile(BueloDslDocument document, CompileOptions options);
}

public record CompileOptions(
    string? HelperClassName = null  // name of generated helpers class, if any
);
```

Output is a valid Sections-mode C# template (starting with `page => {`, `page.Header()...`, etc.) that is then passed to the existing `TemplateEngine` pipeline.

Compilation mappings:
- `report title` → injected before `page.Content()` as a styled header text block
- `page header` → `page.Header().Column(col => { ... })`
- `page footer` → `page.Footer().Column(col => { ... })`
- `data` → wraps content in `page.Content().Column(col => { foreach(var item in data) { ... } })`
- `table` → generates full `page.Content().Table(tbl => { ... })` with column definitions
- `text "{{ expr }}"` → generates `col.Item().Text(x => x.Span(...))` with expression interpolation
- `group header` → generates `tbl.Header(hdr => { ... })` pattern

Expression interpolation: `{{ data.field }}` → C# `(string)data.field`; `{{ FormatCNPJ(data.cnpj) }}` → `BueloGeneratedHelpers.FormatCNPJ((string)data.cnpj)`

---

### 14.5 — `BueloDslEngine` (orchestrator)

File: `Buelo.Engine/BueloDsl/BueloDslEngine.cs`

```csharp
public class BueloDslEngine
{
    // Parse → Compile → render via existing TemplateEngine
    public Task<byte[]> RenderAsync(string source, ReportContext context);
    public (bool valid, IReadOnlyList<BueloDslParseError> errors) Validate(string source);
}
```

Integrated into `TemplateEngine.RenderAsync`: when `mode == BueloDsl`, delegate to `BueloDslEngine`.

---

### 14.6 — Auto-detection update

File: `Buelo.Engine/TemplateEngine.cs` or `SectionsTemplateParser.cs`

Add `BueloDsl` case to mode auto-detection. Heuristics:
- First non-comment, non-blank line matches: `report title:`, `page header:`, `page footer:`, `data:`, `import {`
- Or `@settings` followed by indented properties (without `{` on same line)

---

## Tests

File: `Buelo.Tests/Engine/BueloDslParserTests.cs`
- `Parse_MinimalReport_ReturnsDocument`
- `Parse_ImportStatement_ExtractsFunctionNames`
- `Parse_Settings_ExtractsSizeAndOrientation`
- `Parse_Table_ExtractsColumnsAndGroupHeader`
- `Parse_UnrecognizedKeyword_ReturnsWarning`
- `Parse_UnbalancedExpression_ReturnsError`

File: `Buelo.Tests/Engine/BueloDslCompilerTests.cs`
- `Compile_TextComponent_GeneratesQuestPdfText`
- `Compile_TableWithColumns_GeneratesTableDefinition`
- `Compile_PageHeaderAndFooter_GeneratesCorrectSlots`
- `Compile_ExpressionInterpolation_GeneratesValidCsharp`

File: `Buelo.Tests/Engine/BueloDslEngineTests.cs`
- `RenderAsync_ValidBueloDsl_ReturnsPdfBytes`
- `Validate_ValidSource_ReturnsNoErrors`
- `Validate_MissingDataField_ReturnsError`
