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
    public TemplateMode Mode { get; set; } = TemplateMode.Sections;
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
