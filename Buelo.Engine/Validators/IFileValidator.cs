using Buelo.Contracts;

namespace Buelo.Engine.Validators;

public interface IFileValidator
{
    IReadOnlyList<string> SupportedExtensions { get; }
    Task<FileValidationResult> ValidateAsync(string content);
}
