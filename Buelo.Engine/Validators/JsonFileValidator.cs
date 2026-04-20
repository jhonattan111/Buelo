using Buelo.Contracts;
using System.Text.Json;

namespace Buelo.Engine.Validators;

public class JsonFileValidator : IFileValidator
{
    public IReadOnlyList<string> SupportedExtensions { get; } = [".json"];

    public Task<FileValidationResult> ValidateAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(new FileValidationResult
            {
                Valid = false,
                Errors =
                [
                    new ValidationDiagnostic
                    {
                        Message = "Content is empty.",
                        Line = 1,
                        Column = 1,
                        Severity = "error"
                    }
                ]
            });
        }

        try
        {
            using var _ = JsonDocument.Parse(content, new JsonDocumentOptions { AllowTrailingCommas = false });
            return Task.FromResult(new FileValidationResult { Valid = true });
        }
        catch (JsonException ex)
        {
            // JsonException.LineNumber and BytePositionInLine are 0-based.
            int line = ex.LineNumber.HasValue ? (int)ex.LineNumber.Value + 1 : 1;
            int col = ex.BytePositionInLine.HasValue ? (int)ex.BytePositionInLine.Value + 1 : 1;

            return Task.FromResult(new FileValidationResult
            {
                Valid = false,
                Errors =
                [
                    new ValidationDiagnostic
                    {
                        Message = ex.Message,
                        Line = line,
                        Column = col,
                        Severity = "error"
                    }
                ]
            });
        }
    }
}
