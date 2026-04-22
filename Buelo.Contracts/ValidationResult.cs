namespace Buelo.Contracts;

/// <summary>A single Roslyn diagnostic mapped to user-visible coordinates.</summary>
/// <param name="Message">Human-readable error description.</param>
/// <param name="Line">1-based line number in the generated (wrapped) code.</param>
/// <param name="Column">1-based column number.</param>
public record ValidationError(string Message, int Line, int Column);

/// <summary>Result returned by <c>POST /api/report/validate</c>.</summary>
public record ValidationResult
{
    public bool Valid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];
}

/// <summary>Request body for <c>POST /api/report/validate</c>.</summary>
public class ReportValidateRequest
{
    public string Template { get; set; } = string.Empty;
    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;
}

// ── Per-file-type validation (Sprint 16) ─────────────────────────────────────

/// <summary>A structured diagnostic produced by a per-file-type validator.</summary>
public class ValidationDiagnostic
{
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    /// <summary>"error" | "warning" | "info"</summary>
    public string Severity { get; set; } = "error";
}

/// <summary>Result returned by <c>POST /api/validate</c>.</summary>
public class FileValidationResult
{
    public bool Valid { get; set; }
    public IList<ValidationDiagnostic> Errors { get; set; } = [];
    public IList<ValidationDiagnostic> Warnings { get; set; } = [];
}

/// <summary>Request body for <c>POST /api/validate</c>.</summary>
public class FileValidateRequest
{
    public string Extension { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

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

    public FileValidationResult Result { get; set; } = new();
}
