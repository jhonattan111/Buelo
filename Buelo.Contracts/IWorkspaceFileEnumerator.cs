namespace Buelo.Contracts;

/// <summary>
/// Enumerates all validatable files in the workspace root.
/// </summary>
public interface IWorkspaceFileEnumerator
{
    /// <summary>
    /// Returns all files in the workspace, with their relative paths and raw content.
    /// Only supported file extensions are included.
    /// </summary>
    IAsyncEnumerable<WorkspaceFile> EnumerateAsync();
}

public record WorkspaceFile(string RelativePath, string Extension, string Content);
