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
