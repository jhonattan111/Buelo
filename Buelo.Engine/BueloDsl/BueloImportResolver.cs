using Buelo.Contracts;

namespace Buelo.Engine.BueloDsl;

public record BueloResolvedSource(string EntryPath, string Source, IReadOnlyList<string> OrderedPaths);

public static class BueloImportResolver
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".buelo", ".json", ".cs", ".csx"
    };

    public static async Task<BueloResolvedSource> ResolveAsync(IWorkspaceStore store, string entryPath)
    {
        var ordered = new List<string>();
        var stack = new Stack<string>();
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normalizedEntry = FileSystemWorkspaceStore.NormalizePath(entryPath);
        await VisitAsync(store, normalizedEntry, stack, expanded, ordered, sourceByPath);

        var merged = new List<string>();
        foreach (var path in ordered)
        {
            if (!sourceByPath.TryGetValue(path, out var source))
                continue;

            merged.Add($"# -- import: {path} --");
            merged.Add(source);
            merged.Add(string.Empty);
        }

        return new BueloResolvedSource(normalizedEntry, string.Join('\n', merged), ordered);
    }

    private static async Task VisitAsync(
        IWorkspaceStore store,
        string currentPath,
        Stack<string> stack,
        HashSet<string> expanded,
        List<string> ordered,
        IDictionary<string, string> sourceByPath)
    {
        if (stack.Any(p => string.Equals(p, currentPath, StringComparison.OrdinalIgnoreCase)))
        {
            var cycle = stack.Reverse().Append(currentPath);
            throw new InvalidOperationException($"Import cycle detected: {string.Join(" -> ", cycle)}");
        }

        if (expanded.Contains(currentPath))
            return;

        var file = await store.GetFileAsync(currentPath);
        if (file is null)
            throw new FileNotFoundException($"Imported file '{currentPath}' was not found.", currentPath);

        if (!string.Equals(file.Extension, ".buelo", StringComparison.OrdinalIgnoreCase))
        {
            if (!AllowedExtensions.Contains(file.Extension))
                throw new InvalidOperationException($"Import '{currentPath}' has unsupported extension '{file.Extension}'.");

            expanded.Add(currentPath);
            return;
        }

        stack.Push(currentPath);

        var doc = BueloDslParser.Parse(file.Content);
        foreach (var import in doc.Directives.Imports)
        {
            var resolvedImport = ResolveImportPath(currentPath, import.Source);
            var ext = Path.GetExtension(resolvedImport);

            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException($"Import '{resolvedImport}' has unsupported extension '{ext}'.");

            if (string.Equals(ext, ".buelo", StringComparison.OrdinalIgnoreCase))
            {
                await VisitAsync(store, resolvedImport, stack, expanded, ordered, sourceByPath);
            }
            else
            {
                var exists = await store.ExistsAsync(resolvedImport);
                if (!exists)
                    throw new FileNotFoundException($"Imported file '{resolvedImport}' was not found.", resolvedImport);
            }
        }

        stack.Pop();
        expanded.Add(currentPath);
        ordered.Add(currentPath);
        sourceByPath[currentPath] = file.Content;
    }

    private static string ResolveImportPath(string ownerPath, string importSource)
    {
        if (string.IsNullOrWhiteSpace(importSource))
            throw new InvalidOperationException("Import source cannot be empty.");

        var raw = importSource.Trim().Replace('\\', '/');
        if (raw.StartsWith('/'))
            return FileSystemWorkspaceStore.NormalizePath(raw);

        if (raw.StartsWith("./", StringComparison.Ordinal))
            raw = raw[2..];

        var ownerDir = ownerPath.Contains('/')
            ? ownerPath[..ownerPath.LastIndexOf('/')]
            : string.Empty;

        var combined = string.IsNullOrWhiteSpace(ownerDir) ? raw : $"{ownerDir}/{raw}";
        return FileSystemWorkspaceStore.NormalizePath(combined);
    }
}
