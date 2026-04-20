using Buelo.Contracts;

namespace Buelo.Engine;

/// <summary>
/// Enumerates workspace files from the file-system template store root.
/// </summary>
public class FileSystemWorkspaceFileEnumerator(string root) : IWorkspaceFileEnumerator
{
    private static readonly HashSet<string> ValidatableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".buelo", ".json", ".csx", ".cs"
    };

    public async IAsyncEnumerable<WorkspaceFile> EnumerateAsync()
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!ValidatableExtensions.Contains(ext))
                continue;

            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "template.record.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var content = await File.ReadAllTextAsync(file);
            yield return new WorkspaceFile(relative, ext, content);
        }
    }
}
