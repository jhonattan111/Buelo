# Sprint 22 (Backend) — Typed Data + C# IntelliSense Support

## Goal

Enable the Monaco editor on the frontend to offer proper **IntelliSense for `data` properties** when
the user has a `.json` file bound as data source. The backend analyses the JSON structure at the
workspace path and returns C# type declarations that the frontend injects into Monaco as an extra
read-only model, giving the Roslyn syntax checker and word-complete engine enough context to suggest
`data.Employees[0].Name`, `data.Total`, etc.

Also covers: **report settings persistence** — replacing `sessionStorage` (lost on tab close) with
`localStorage` so the `dataSourcePath` and all per-file settings survive full page reloads.

## Status

`[ ] planned`

## Dependencies

- Sprint 21 backend complete ✅ (workspace filesystem + `WorkspaceController`)
- Sprint 22 frontend workspace UX complete ✅ (multi-tabs, data source picker)

---

## Scope

### In scope

- JSON schema inference from workspace files
- C# typed class/record generation from inferred schema
- New endpoint exposing generated type declarations
- Document type-injection conventions for MonacoEditor

### Out of scope

- Full Roslyn language server / LSP integration
- Type generation from external / uploaded JSON schemas (JSON Schema `.json`)
- Recursive circular reference detection beyond depth-10 guard
- Automated round-trip tests with Monaco (frontend unit test)

---

## Tasks

### BE-22.1 — `JsonTypeInferrer` — JSON → C# type declarations

**File:** `Buelo.Engine/JsonTypeInferrer.cs` (new)

Implement a static service that walks a `JsonElement` tree and emits C# `record` declarations:

```
Rules:
- JSON object  → record with positional properties (nullable-aware)
- JSON array   → T[] where T is inferred from first element (or object[])
- string       → string
- number (int) → int   (no fractional component)
- number (dec) → double (has fractional component)
- bool         → bool
- null / mixed → object?

Naming:
- Root object  → public record DataModel(...)
- Nested obj   → public record <ParentProp>Model(...)
                  (PascalCase from JSON key)
- Arrays of obj→ public record <Prop>Item(...)

Depth guard:
- Stop recursion at depth 10; emit object? for deeper values.

Output:
- A single string containing all record declarations, ordered
  deepest-first so each type is declared before it is referenced.
```

Signature:

```csharp
namespace Buelo.Engine;

public static class JsonTypeInferrer
{
    /// <summary>
    /// Infers C# record declarations from a JSON string.
    /// Returns a multi-line C# source fragment suitable for injection into Monaco
    /// as an extra read-only model (no namespace wrapper needed).
    /// </summary>
    public static string InferCSharpTypes(string json, string rootTypeName = "DataModel");
}
```

Example input JSON:

```json
{
  "name": "Buelo Corp",
  "employees": [
    { "id": 1, "name": "Ana", "active": true }
  ],
  "total": 3.14
}
```

Expected output:

```csharp
public record EmployeesItem(int Id, string Name, bool Active);
public record DataModel(string Name, EmployeesItem[] Employees, double Total);
```

---

### BE-22.2 — `WorkspaceController` — `GET /api/workspace/types`

**File:** `Buelo.Api/Controllers/WorkspaceController.cs` (extend)

Add a new endpoint:

```
GET /api/workspace/types?path=data/employees.json
```

Behaviour:
1. Resolve `path` against the workspace root (same traversal guard as existing file endpoints).
2. Read the file content; expect valid JSON.
3. Call `JsonTypeInferrer.InferCSharpTypes(content)` → returns C# fragment.
4. Return `200 OK` with `application/json` body:

```json
{
  "path": "data/employees.json",
  "csharpDeclarations": "public record ...\npublic record DataModel(...);"
}
```

Error cases:
- `404` if path does not exist.
- `400` if file is not valid JSON (include parse error message).
- `400` if path extension is not `.json`.

Controller method signature:

```csharp
[HttpGet("types")]
public async Task<IActionResult> GetTypeDeclarations([FromQuery] string path)
```

---

### BE-22.3 — `PageSettings` persistence contract clarification

**File:** `Buelo.Contracts/PageSettings.cs`

No new properties needed. Verify that `DataSourcePath` is serialised correctly by `System.Text.Json`
(it already is). Add an XML doc note:

```csharp
/// <remarks>
/// Stored per-file in frontend localStorage under key <c>buelo.reportSettings</c>.
/// Sent to the render endpoint as part of <see cref="ReportRequest.PageSettings"/>.
/// </remarks>
```

---

### BE-22.4 — Tests

**File:** `Buelo.Tests/Engine/JsonTypeInferrerTests.cs` (new)

Cover:
- Flat object with all primitive types
- Nested object → emits two records
- Array of objects → emits item record + parent record
- Empty array → emits `object[]` for the property type
- Null value → emits `object?`
- Depth guard: object nested 11 levels deep emits `object?` at level 10
- Invalid JSON → `JsonException` propagated or returned as error string (match chosen impl)

---

## Acceptance Criteria

1. `GET /api/workspace/types?path=data/mock.json` returns valid C# record declarations reflecting
   the JSON structure.
2. Nested objects produce named inner records; arrays produce `<Prop>Item[]` arrays.
3. Depth guard prevents stack overflow on pathological inputs.
4. All `JsonTypeInferrerTests` pass.
5. Endpoint returns `400` for non-`.json` paths and `404` for missing files.

---
