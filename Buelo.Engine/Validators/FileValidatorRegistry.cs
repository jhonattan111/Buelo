using Buelo.Contracts;

namespace Buelo.Engine.Validators;

public class FileValidatorRegistry
{
    private readonly IReadOnlyList<IFileValidator> _validators;

    public FileValidatorRegistry(IEnumerable<IFileValidator> validators)
    {
        _validators = [.. validators];
    }

    public IFileValidator? GetValidator(string extension)
        => _validators.FirstOrDefault(v =>
            v.SupportedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)));

    public async Task<FileValidationResult> ValidateAsync(string extension, string content)
    {
        var validator = GetValidator(extension);
        if (validator is null)
        {
            return new FileValidationResult
            {
                Valid = true,
                Warnings =
                [
                    new ValidationDiagnostic
                    {
                        Message = $"No validator available for extension '{extension}'.",
                        Line = 0,
                        Column = 0,
                        Severity = "info"
                    }
                ]
            };
        }

        // CsharpFileValidator supports both .cs and .csx with different parse modes.
        if (validator is CsharpFileValidator csValidator)
            return await csValidator.ValidateWithExtensionAsync(content, extension);

        return await validator.ValidateAsync(content);
    }
}
