using Buelo.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buelo.Engine;

/// <summary>
/// File-system backed implementation of <see cref="IGlobalArtefactStore"/>.
/// <para>
/// Global artefacts are stored in a flat <c>_global/</c> subdirectory under the configured root:
/// <code>
/// {root}/_global/
///   colaborador.json
///   colaborador.json.meta.json     — { id, description, tags, createdAt, updatedAt }
///   shared-header.buelo
///   shared-header.buelo.meta.json
///   formatters.csx
///   formatters.csx.meta.json
/// </code>
/// </para>
/// </summary>
public class FileSystemGlobalArtefactStore : IGlobalArtefactStore
{
    private const string GlobalDir = "_global";
    private const string MetaSuffix = ".meta.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _globalDir;

    public FileSystemGlobalArtefactStore(string root)
    {
        _globalDir = Path.Combine(root, GlobalDir);
        Directory.CreateDirectory(_globalDir);
    }

    public async Task<GlobalArtefact?> GetAsync(Guid id)
    {
        foreach (var metaPath in Directory.EnumerateFiles(_globalDir, $"*{MetaSuffix}"))
        {
            var meta = await ReadMetaAsync(metaPath);
            if (meta is not null && meta.Id == id)
            {
                var contentPath = metaPath[..^MetaSuffix.Length];
                return await BuildArtefactAsync(meta, contentPath);
            }
        }
        return null;
    }

    public async Task<GlobalArtefact?> GetByNameAsync(string name, string extension)
    {
        var fileName = name + extension;
        var contentPath = Path.Combine(_globalDir, fileName);
        if (!File.Exists(contentPath))
            return null;

        var metaPath = contentPath + MetaSuffix;
        var meta = await ReadMetaAsync(metaPath);
        if (meta is null)
            return null;

        return await BuildArtefactAsync(meta, contentPath);
    }

    public async Task<IReadOnlyList<GlobalArtefact>> ListAsync(string? extensionFilter = null)
    {
        var results = new List<GlobalArtefact>();
        foreach (var metaPath in Directory.EnumerateFiles(_globalDir, $"*{MetaSuffix}"))
        {
            var contentPath = metaPath[..^MetaSuffix.Length];
            if (!File.Exists(contentPath))
                continue;

            var ext = Path.GetExtension(contentPath);
            if (extensionFilter is not null && !string.Equals(ext, extensionFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var meta = await ReadMetaAsync(metaPath);
            if (meta is null)
                continue;

            results.Add(await BuildArtefactAsync(meta, contentPath));
        }
        return results;
    }

    public async Task<GlobalArtefact> SaveAsync(GlobalArtefact artefact)
    {
        var now = DateTimeOffset.UtcNow;

        if (artefact.Id == Guid.Empty)
        {
            artefact.Id = Guid.NewGuid();
            artefact.CreatedAt = now;
        }

        artefact.UpdatedAt = now;

        var fileName = artefact.Name + artefact.Extension;
        var contentPath = Path.Combine(_globalDir, fileName);
        var metaPath = contentPath + MetaSuffix;

        await File.WriteAllTextAsync(contentPath, artefact.Content);

        var meta = new ArtefactMeta(
            artefact.Id,
            artefact.Name,
            artefact.Extension,
            artefact.Description,
            artefact.Tags,
            artefact.CreatedAt,
            artefact.UpdatedAt);

        var json = JsonSerializer.Serialize(meta, JsonOpts);
        await File.WriteAllTextAsync(metaPath, json);

        return artefact;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        foreach (var metaPath in Directory.EnumerateFiles(_globalDir, $"*{MetaSuffix}"))
        {
            var json = File.ReadAllText(metaPath);
            ArtefactMeta? meta;
            try { meta = JsonSerializer.Deserialize<ArtefactMeta>(json, JsonOpts); }
            catch { continue; }

            if (meta?.Id != id)
                continue;

            var contentPath = metaPath[..^MetaSuffix.Length];
            if (File.Exists(contentPath))
                File.Delete(contentPath);
            File.Delete(metaPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<ArtefactMeta?> ReadMetaAsync(string metaPath)
    {
        if (!File.Exists(metaPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(metaPath);
            return JsonSerializer.Deserialize<ArtefactMeta>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<GlobalArtefact> BuildArtefactAsync(ArtefactMeta meta, string contentPath)
    {
        var content = File.Exists(contentPath)
            ? await File.ReadAllTextAsync(contentPath)
            : string.Empty;

        return new GlobalArtefact
        {
            Id = meta.Id,
            Name = meta.Name,
            Extension = meta.Extension,
            Content = content,
            Description = meta.Description,
            Tags = meta.Tags ?? [],
            CreatedAt = meta.CreatedAt,
            UpdatedAt = meta.UpdatedAt,
        };
    }

    // ── Private model ─────────────────────────────────────────────────────────

    private record ArtefactMeta(
        Guid Id,
        string Name,
        string Extension,
        string? Description,
        IList<string>? Tags,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
