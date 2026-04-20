using Buelo.Contracts;
using Buelo.Engine.BueloDsl;

namespace Buelo.Engine.Validators;

public class BueloDslValidator : IFileValidator
{
    public IReadOnlyList<string> SupportedExtensions { get; } = [".buelo"];

    public Task<FileValidationResult> ValidateAsync(string content)
    {
        var doc = BueloDslParser.Parse(content, out var parseErrors);

        var errors = new List<ValidationDiagnostic>();
        var warnings = new List<ValidationDiagnostic>();

        // Map parse errors and warnings.
        foreach (var e in parseErrors)
        {
            var diag = new ValidationDiagnostic
            {
                Message = e.Message,
                Line = e.Line,
                Column = e.Column,
                Severity = e.Severity == BueloDslErrorSeverity.Error ? "error" : "warning"
            };
            if (e.Severity == BueloDslErrorSeverity.Error)
                errors.Add(diag);
            else
                warnings.Add(diag);
        }

        // Warn if a "data" component block exists but no @data from directive is set.
        bool hasDataComponent = doc.Components.Any(c =>
            c is BueloDslLayoutComponent lc &&
            string.Equals(lc.ComponentType, "data", StringComparison.OrdinalIgnoreCase));
        if (hasDataComponent && doc.Directives.DataRef is null)
        {
            warnings.Add(new ValidationDiagnostic
            {
                Message = "A 'data' block is defined but no '@data from' directive was found. The data source will not be bound.",
                Line = 0,
                Column = 0,
                Severity = "warning"
            });
        }

        // Error if the same top-level layout component type appears more than once.
        var layoutTypes = doc.Components
            .OfType<BueloDslLayoutComponent>()
            .GroupBy(c => c.ComponentType, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in layoutTypes)
        {
            errors.Add(new ValidationDiagnostic
            {
                Message = $"Duplicate layout component: '{group.Key}'. Each layout section may appear only once.",
                Line = 0,
                Column = 0,
                Severity = "error"
            });
        }

        return Task.FromResult(new FileValidationResult
        {
            Valid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        });
    }
}
