# Sprint 19 — Backend: Project-wide Validation

## Goal
Replace per-file validation with a **project-wide validation** endpoint. Instead of validating a
single open file, the system validates every file in the workspace and returns an aggregated
report grouped by file path. This gives users a full picture of the project health in one action.

## Status
`[x] done`

## Dependencies
- Sprint 16 complete ✅ (`FileValidatorRegistry`, per-extension validators)
- Sprint 18 backend complete ✅ (no `IBueloProjectStore`; workspace is flat file system)

---

## Tasks

### 19-B.1 — `ProjectValidationResult` model

File: `Buelo.Contracts/ValidationResult.cs` (extend existing file)

Add:

```csharp
/// <summary>
/// Aggregated validation result for all files in the workspace.
/// </summary>
public class ProjectValidationResult
{
    /// <summary>True only when every file in the project is valid.</summary>
    public bool Valid => Files.All(f => f.Result.Valid);

    /// <summary>Per-file validation results, ordered by file path.</summary>
    public IList<FileValidationEntry> Files { get; set; } = [];

    /// <summary>Total number of errors across all files.</summary>
    public int TotalErrors => Files.Sum(f => f.Result.Errors.Count);

    /// <summary>Total number of warnings across all files.</summary>
    public int TotalWarnings => Files.Sum(f => f.Result.Warnings.Count);
}

/// <summary>Validation result for a single workspace file.</summary>
public class FileValidationEntry
{
    /// <summary>Workspace-relative file path, e.g. "relatorio_1/relatorio_1.buelo".</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>File extension (e.g. ".buelo", ".json", ".csx").</summary>
    public string Extension { get; set; } = string.Empty;

    public ValidationResult Result { get; set; } = new();
}
```

---

### 19-B.2 — `IWorkspaceFileEnumerator` service

File: `Buelo.Contracts/IWorkspaceFileEnumerator.cs` (new file)

```csharp
/// <summary>
/// Enumerates all validatable files in the workspace root.
/// </summary>
public interface IWorkspaceFileEnumerator
{
    /// <summary>
    /// Returns all files in the workspace, with their relative paths and raw content.
    /// Only file extensions supported by <see cref="FileValidatorRegistry"/> are included.
    /// </summary>
    IAsyncEnumerable<WorkspaceFile> EnumerateAsync();
}

public record WorkspaceFile(string RelativePath, string Extension, string Content);
```

---

### 19-B.3 — `FileSystemWorkspaceFileEnumerator`

File: `Buelo.Engine/FileSystemWorkspaceFileEnumerator.cs` (new file)

```csharp
/// <summary>
/// Enumerates workspace files from the file system template store root.
/// </summary>
public class FileSystemWorkspaceFileEnumerator(string root) : IWorkspaceFileEnumerator
{
    private static readonly HashSet<string> ValidatableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".buelo", ".json", ".csx", ".cs"
    };

    public async IAsyncEnumerable<WorkspaceFile> EnumerateAsync()
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!ValidatableExtensions.Contains(ext)) continue;

            // Skip internal metadata files
            var fileName = Path.GetFileName(file);
            if (fileName is "template.record.json") continue;

            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var content  = await File.ReadAllTextAsync(file);
            yield return new WorkspaceFile(relative, ext, content);
        }
    }
}
```

---

### 19-B.4 — `POST /api/validate/project` endpoint

File: `Buelo.Api/Controllers/ValidateController.cs`

Add a second action:

```csharp
[HttpPost("project")]
public async Task<IActionResult> ValidateProject()
{
    var result = new ProjectValidationResult();

    await foreach (var file in enumerator.EnumerateAsync())
    {
        var fileResult = await registry.ValidateAsync(file.Extension, file.Content);
        result.Files.Add(new FileValidationEntry
        {
            Path      = file.RelativePath,
            Extension = file.Extension,
            Result    = fileResult,
        });
    }

    result.Files = result.Files.OrderBy(f => f.Path).ToList();
    return Ok(result);
}
```

Inject `IWorkspaceFileEnumerator enumerator` into the controller constructor.

---

### 19-B.5 — Register `IWorkspaceFileEnumerator` in DI

File: `Buelo.Engine/EngineExtensions.cs`

```csharp
services.AddSingleton<IWorkspaceFileEnumerator>(
    _ => new FileSystemWorkspaceFileEnumerator(templateStorePath));
```

---

### 19-B.6 — Tests

File: `Buelo.Tests/Engine/ProjectValidationTests.cs` (new file)

- Given a workspace with one valid `.buelo` and one invalid `.json`: result has 2 entries, `Valid == false`.
- Given a workspace with all valid files: `Valid == true`, `TotalErrors == 0`.
- Files are ordered alphabetically by path in the result.

---

## API Contract

```
POST /api/validate/project
Content-Type: (no body required)

Response 200:
{
  "valid": false,
  "totalErrors": 2,
  "totalWarnings": 0,
  "files": [
    {
      "path": "relatorio_1/relatorio_1.buelo",
      "extension": ".buelo",
      "result": {
        "valid": false,
        "errors": [
          { "message": "Unknown component 'chrt'", "line": 12, "column": 1, "severity": "error" }
        ],
        "warnings": []
      }
    },
    {
      "path": "data/colaboradores.json",
      "extension": ".json",
      "result": { "valid": true, "errors": [], "warnings": [] }
    }
  ]
}
```
