using Buelo.Contracts;

namespace Buelo.Engine;

public class FileSystemWorkspaceStore : IWorkspaceStore
{
    private static readonly HashSet<string> InternalFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "template.record.json"
    };

    private readonly string _root;

    public FileSystemWorkspaceStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public Task<IReadOnlyList<WorkspaceNode>> GetTreeAsync()
    {
        var nodes = BuildTree(_root, prefix: string.Empty);
        return Task.FromResult<IReadOnlyList<WorkspaceNode>>(nodes);
    }

    public async Task<IReadOnlyList<WorkspaceFileRecord>> ListFilesAsync(string? extension = null)
    {
        var files = new List<WorkspaceFileRecord>();
        var wantedExt = string.IsNullOrWhiteSpace(extension) ? null : NormalizeExtension(extension);

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            if (ShouldIgnore(file))
                continue;

            var ext = Path.GetExtension(file);
            if (wantedExt is not null && !string.Equals(ext, wantedExt, StringComparison.OrdinalIgnoreCase))
                continue;

            files.Add(await ReadFileRecordAsync(file));
        }

        files.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    public async Task<WorkspaceFileRecord?> GetFileAsync(string path)
    {
        var fullPath = ResolveFilePath(path);
        if (!File.Exists(fullPath))
            return null;

        if (ShouldIgnore(fullPath))
            return null;

        return await ReadFileRecordAsync(fullPath);
    }

    public async Task<WorkspaceFileRecord> CreateFileAsync(string path, string content = "", bool overwrite = false)
    {
        var normalized = NormalizePath(path);
        var fullPath = ResolveFilePath(normalized);

        if (Directory.Exists(fullPath))
            throw new InvalidOperationException($"Path '{normalized}' points to an existing folder.");

        if (File.Exists(fullPath) && !overwrite)
            throw new InvalidOperationException($"File '{normalized}' already exists.");

        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);

        await File.WriteAllTextAsync(fullPath, content);
        return await ReadFileRecordAsync(fullPath);
    }

    public async Task<WorkspaceFileRecord> UpdateFileAsync(string path, string content, bool createIfMissing = false)
    {
        var normalized = NormalizePath(path);
        var fullPath = ResolveFilePath(normalized);

        if (!File.Exists(fullPath))
        {
            if (!createIfMissing)
                throw new FileNotFoundException($"File '{normalized}' not found.", normalized);

            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
        }

        await File.WriteAllTextAsync(fullPath, content);
        return await ReadFileRecordAsync(fullPath);
    }

    public Task CreateFolderAsync(string path)
    {
        var normalized = NormalizePath(path);
        var fullPath = ResolveFilePath(normalized);

        if (File.Exists(fullPath))
            throw new InvalidOperationException($"Path '{normalized}' points to an existing file.");

        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    public Task MoveAsync(string path, string destinationPath, bool overwrite = false)
    {
        var source = NormalizePath(path);
        var destination = NormalizePath(destinationPath);
        var sourceFull = ResolveFilePath(source);
        var destinationFull = ResolveFilePath(destination);

        if (!File.Exists(sourceFull) && !Directory.Exists(sourceFull))
            throw new FileNotFoundException($"Source '{source}' not found.", source);

        if (File.Exists(destinationFull) || Directory.Exists(destinationFull))
        {
            if (!overwrite)
                throw new InvalidOperationException($"Destination '{destination}' already exists.");

            if (File.Exists(destinationFull))
                File.Delete(destinationFull);
            else
                Directory.Delete(destinationFull, recursive: true);
        }

        var parent = Path.GetDirectoryName(destinationFull);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);

        if (File.Exists(sourceFull))
            File.Move(sourceFull, destinationFull);
        else
            Directory.Move(sourceFull, destinationFull);

        return Task.CompletedTask;
    }

    public Task RenameAsync(string path, string newName, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("New name must not be empty.");

        var normalized = NormalizePath(path);
        var parent = GetParentPath(normalized);
        var destination = string.IsNullOrWhiteSpace(parent) ? newName.Trim() : $"{parent}/{newName.Trim()}";
        return MoveAsync(normalized, destination, overwrite);
    }

    public Task DeleteAsync(string path, bool recursive = true)
    {
        var normalized = NormalizePath(path);
        var fullPath = ResolveFilePath(normalized);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.CompletedTask;
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive);
            return Task.CompletedTask;
        }

        throw new FileNotFoundException($"Path '{normalized}' not found.", normalized);
    }

    public Task<bool> ExistsAsync(string path)
    {
        var normalized = NormalizePath(path);
        var fullPath = ResolveFilePath(normalized);
        return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
    }

    private List<WorkspaceNode> BuildTree(string directory, string prefix)
    {
        var nodes = new List<WorkspaceNode>();

        foreach (var sub in Directory.EnumerateDirectories(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (ShouldIgnore(sub))
                continue;

            var name = Path.GetFileName(sub);
            var path = string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix}/{name}";
            var children = BuildTree(sub, path);

            nodes.Add(new WorkspaceNode
            {
                Path = path,
                Name = name,
                Type = "folder",
                Kind = "folder",
                Extension = string.Empty,
                Children = children
            });
        }

        foreach (var file in Directory.EnumerateFiles(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (ShouldIgnore(file))
                continue;

            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(file);
            var path = string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix}/{name}";

            nodes.Add(new WorkspaceNode
            {
                Path = path,
                Name = name,
                Type = "file",
                Extension = ext,
                Kind = InferKind(ext),
                Children = []
            });
        }

        return nodes;
    }

    private static string InferKind(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".buelo" => "report",
            ".json" => "data",
            ".cs" => "helper",
            ".csx" => "helper",
            _ => "file"
        };
    }

    private bool ShouldIgnore(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        if (InternalFileNames.Contains(fileName))
            return true;

        var relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
        if (relative.StartsWith("versions/", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private async Task<WorkspaceFileRecord> ReadFileRecordAsync(string fullPath)
    {
        var info = new FileInfo(fullPath);
        var content = await File.ReadAllTextAsync(fullPath);
        var relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');

        return new WorkspaceFileRecord
        {
            Path = relative,
            Name = info.Name,
            Extension = info.Extension,
            Content = content,
            LastModifiedUtc = info.LastWriteTimeUtc
        };
    }

    private string ResolveFilePath(string path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return _root;

        return Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetParentPath(string path)
    {
        var idx = path.LastIndexOf('/');
        if (idx <= 0)
            return string.Empty;

        return path[..idx];
    }

    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim().Replace('\\', '/');
        while (trimmed.StartsWith('/'))
            trimmed = trimmed[1..];

        if (Path.IsPathRooted(trimmed))
            throw new InvalidOperationException($"Absolute path is not allowed: '{path}'.");

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException($"Invalid path traversal segment in '{path}'.");

        if (segments.Any(s => s.Contains(':')))
            throw new InvalidOperationException($"Invalid path segment in '{path}'.");

        return string.Join('/', segments);
    }

    private static string NormalizeExtension(string extension)
    {
        var value = extension.Trim();
        if (!value.StartsWith('.'))
            value = "." + value;

        return value;
    }
}
