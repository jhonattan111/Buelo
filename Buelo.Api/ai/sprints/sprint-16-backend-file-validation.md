# Sprint 16 — Backend: Per-File Type Validation

## Goal
Provide server-side validation for every file type the workspace handles. The frontend can POST any file's content with its extension to get structured errors back — line numbers, columns, severity — which are then used to display Monaco squiggles. Each validator is focused: `.buelo` validates DSL syntax, `.json` validates JSON structure, and `.cs`/`.csx` validates C# syntax via Roslyn without full compilation.

## Status
`[ ] pending`

## Dependencies
- Sprint 14 complete ✅ (`BueloDslParser` with error reporting available)
- Roslyn (`Microsoft.CodeAnalysis.CSharp`) already in project ✅

---

## Unified Validation Contract

### Request

`POST /api/validate`

```json
{
  "extension": ".buelo",
  "content": "report title:\n  text: Hello"
}
```

Supported extensions: `.buelo`, `.json`, `.cs`, `.csx`

### Response

```json
{
  "valid": true,
  "errors": [],
  "warnings": []
}
```

Error/warning object:
```json
{
  "message": "Unrecognized component keyword: 'tabel'",
  "line": 5,
  "column": 1,
  "severity": "error"
}
```

`severity` values: `"error"` | `"warning"` | `"info"`

---

## Backend Scope

### 16.1 — `ValidationResult` update

File: `Buelo.Contracts/ValidationResult.cs`

Extend existing model (or create new `FileValidationResult` if keeping old contract stable):

```csharp
public class FileValidationResult
{
    public bool Valid { get; set; }
    public IList<ValidationDiagnostic> Errors { get; set; } = [];
    public IList<ValidationDiagnostic> Warnings { get; set; } = [];
}

public class ValidationDiagnostic
{
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Severity { get; set; } = "error"; // "error" | "warning" | "info"
}
```

---

### 16.2 — `IFileValidator` interface

File: `Buelo.Engine/Validators/IFileValidator.cs`

```csharp
public interface IFileValidator
{
    IReadOnlyList<string> SupportedExtensions { get; }
    Task<FileValidationResult> ValidateAsync(string content);
}
```

---

### 16.3 — `BueloDslValidator`

File: `Buelo.Engine/Validators/BueloDslValidator.cs`

Implements `IFileValidator`. Supported extensions: `[".buelo"]`.

Delegates to `BueloDslParser.Parse(content, out errors)` from Sprint 14.

Maps `BueloDslParseError` to `ValidationDiagnostic`:
- `BueloDslErrorSeverity.Error` → `severity: "error"`
- `BueloDslErrorSeverity.Warning` → `severity: "warning"`

Additional checks (beyond parser):
- Warn if `data:` block exists but no `@data from` directive is present
- Warn if `import { Func }` references an unknown function (requires access to global artefact store — inject `IGlobalArtefactStore`)
- Error if same layout component appears more than once (e.g., two `page header:` blocks)

---

### 16.4 — `JsonFileValidator`

File: `Buelo.Engine/Validators/JsonFileValidator.cs`

Implements `IFileValidator`. Supported extensions: `[".json"]`.

Uses `System.Text.Json.JsonDocument.Parse(content)` in a try/catch:
- `JsonException` → parse the `LineNumber`/`BytePositionInLine` from the exception message
- Reports single error with location if invalid
- Reports no errors if valid (deep structural validation is out of scope — that's the schema's job)

---

### 16.5 — `CsharpFileValidator`

File: `Buelo.Engine/Validators/CsharpFileValidator.cs`

Implements `IFileValidator`. Supported extensions: `[".cs", ".csx"]`.

For `.cs` files: uses `CSharpSyntaxTree.ParseText(content)` and inspects `GetDiagnostics()`.  
For `.csx` files: uses `CSharpSyntaxTree.ParseText(content, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script))`.

Maps Roslyn `Diagnostic` → `ValidationDiagnostic`:
- `DiagnosticSeverity.Error` → `"error"`
- `DiagnosticSeverity.Warning` → `"warning"`
- `DiagnosticSeverity.Info` → `"info"`
- Hidden → skip

Line/column: extracted from `diagnostic.Location.GetLineSpan().StartLinePosition` (0-based → add 1 for 1-based output).

**Note**: only syntax errors are reported; semantic errors (undefined types, missing references) require full compilation and are out of scope for this validator.

---

### 16.6 — `FileValidatorRegistry`

File: `Buelo.Engine/Validators/FileValidatorRegistry.cs`

```csharp
public class FileValidatorRegistry
{
    public FileValidatorRegistry(IEnumerable<IFileValidator> validators);
    public IFileValidator? GetValidator(string extension);
    public Task<FileValidationResult> ValidateAsync(string extension, string content);
}
```

`GetValidator`: matches by extension (case-insensitive). Returns `null` if no validator supports the extension.

---

### 16.7 — Validation endpoint

File: `Buelo.Api/Controllers/ReportController.cs` or new `ValidateController.cs`

```
POST /api/validate
```

Body: `{ extension: string, content: string }`

Behavior:
1. Look up `FileValidatorRegistry` by extension
2. If no validator found: return `{ valid: true, errors: [], warnings: [{ message: "No validator available for extension", severity: "info" }] }`
3. Run validator and return result
4. Do NOT throw on content errors — always return 200 with the error list

Keep existing `POST /api/report/validate` endpoint unchanged (Roslyn full-compile for Sections mode) — it is separate from this file-type validator.

---

### 16.8 — DI registration

File: `Buelo.Engine/EngineExtensions.cs`

```csharp
services.AddSingleton<IFileValidator, BueloDslValidator>();
services.AddSingleton<IFileValidator, JsonFileValidator>();
services.AddSingleton<IFileValidator, CsharpFileValidator>();
services.AddSingleton<FileValidatorRegistry>();
```

---

## Tests

File: `Buelo.Tests/Engine/BueloDslValidatorTests.cs`
- `Validate_ValidBueloDsl_ReturnsNoErrors`
- `Validate_UnrecognizedComponent_ReturnsWarning`
- `Validate_DuplicatePageHeader_ReturnsError`
- `Validate_DataBlockWithoutDataDirective_ReturnsWarning`
- `Validate_UnbalancedExpression_ReturnsError`

File: `Buelo.Tests/Engine/JsonFileValidatorTests.cs`
- `Validate_ValidJson_ReturnsNoErrors`
- `Validate_InvalidJson_ReturnsErrorWithLineNumber`
- `Validate_EmptyString_ReturnsError`

File: `Buelo.Tests/Engine/CsharpFileValidatorTests.cs`
- `Validate_ValidCsharpClass_ReturnsNoErrors`
- `Validate_ValidCsxScript_ReturnsNoErrors`
- `Validate_MissingSemicolon_ReturnsError`
- `Validate_InvalidSyntax_ReturnsErrorWithPosition`

File: `Buelo.Tests/Api/ValidateControllerTests.cs`
- `PostValidate_BueloExtension_RoutesToBueloDslValidator`
- `PostValidate_UnknownExtension_ReturnsInfoWarning`
- `PostValidate_Always200_EvenWithErrors`
