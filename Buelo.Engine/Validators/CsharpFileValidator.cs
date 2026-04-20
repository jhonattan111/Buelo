using Buelo.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Buelo.Engine.Validators;

public class CsharpFileValidator : IFileValidator
{
    public IReadOnlyList<string> SupportedExtensions { get; } = [".cs", ".csx"];

    public Task<FileValidationResult> ValidateAsync(string content)
    {
        // Intentionally not used — caller passes extension via ValidateAsync(string extension, string content)
        // but IFileValidator.ValidateAsync only receives content. Default to regular C# class syntax.
        return ValidateWithExtensionAsync(content, ".cs");
    }

    public Task<FileValidationResult> ValidateWithExtensionAsync(string content, string extension)
    {
        var kind = string.Equals(extension, ".csx", StringComparison.OrdinalIgnoreCase)
            ? SourceCodeKind.Script
            : SourceCodeKind.Regular;

        var parseOptions = CSharpParseOptions.Default.WithKind(kind);
        var tree = CSharpSyntaxTree.ParseText(content, parseOptions);
        var diagnostics = tree.GetDiagnostics();

        var errors = new List<ValidationDiagnostic>();
        var warnings = new List<ValidationDiagnostic>();

        foreach (var d in diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Hidden) continue;

            var span = d.Location.GetLineSpan();
            var diag = new ValidationDiagnostic
            {
                Message = d.GetMessage(),
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                Severity = d.Severity switch
                {
                    DiagnosticSeverity.Error => "error",
                    DiagnosticSeverity.Warning => "warning",
                    _ => "info"
                }
            };

            if (d.Severity == DiagnosticSeverity.Error)
                errors.Add(diag);
            else
                warnings.Add(diag);
        }

        return Task.FromResult(new FileValidationResult
        {
            Valid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        });
    }
}
