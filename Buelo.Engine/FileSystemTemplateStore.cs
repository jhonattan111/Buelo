using Buelo.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buelo.Engine;

/// <summary>
/// File-system backed implementation of <see cref="ITemplateStore"/>.
/// <para>
/// Each template lives in its own sub-directory under <paramref name="root"/>:
/// <code>
/// {root}/{id}/
///   template.record.json   — metadata (no Template source, no Artefacts)
///   template.report.cs     — template source code
///   {name}{ext}            — each artefact (e.g. mockdata.json, helper-tax.cs)
/// </code>
/// </para>
/// <para>Opt into this store via <c>builder.Services.AddBueloFileSystemStore()</c>.</para>
/// </summary>
public class FileSystemTemplateStore : ITemplateStore
{
    private const string MetaFile = "template.record.json";
    private const string SourceFile = "template.report.cs";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _root;

    public FileSystemTemplateStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc/>
    public async Task<TemplateRecord?> GetAsync(Guid id)
    {
        var dir = TemplateDir(id);
        if (!Directory.Exists(dir))
            return null;

        return await ReadRecordAsync(dir);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TemplateRecord>> ListAsync()
    {
        var results = new List<TemplateRecord>();
        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            var record = await ReadRecordAsync(dir);
            if (record is not null)
                results.Add(record);
        }
        return results;
    }

    /// <inheritdoc/>
    public async Task<TemplateRecord> SaveAsync(TemplateRecord template)
    {
        if (template.Id == Guid.Empty)
        {
            template.Id = Guid.NewGuid();
            template.CreatedAt = DateTimeOffset.UtcNow;
        }
        template.UpdatedAt = DateTimeOffset.UtcNow;

        var dir = TemplateDir(template.Id);
        Directory.CreateDirectory(dir);

        // Write metadata (excluding Template source and Artefacts).
        var meta = ToMeta(template);
        var metaJson = JsonSerializer.Serialize(meta, JsonOpts);
        await File.WriteAllTextAsync(Path.Combine(dir, MetaFile), metaJson);

        // Write template source.
        await File.WriteAllTextAsync(Path.Combine(dir, SourceFile), template.Template ?? string.Empty);

        // Delete artefact files that are no longer present.
        var keepFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MetaFile,
            SourceFile
        };
        foreach (var a in template.Artefacts)
            keepFiles.Add($"{a.Name}{a.Extension}");

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var fileName = Path.GetFileName(file);
            if (!keepFiles.Contains(fileName))
                File.Delete(file);
        }

        // Write each artefact.
        foreach (var a in template.Artefacts)
            await File.WriteAllTextAsync(Path.Combine(dir, $"{a.Name}{a.Extension}"), a.Content);

        return template;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(Guid id)
    {
        var dir = TemplateDir(id);
        if (!Directory.Exists(dir))
            return Task.FromResult(false);

        Directory.Delete(dir, recursive: true);
        return Task.FromResult(true);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string TemplateDir(Guid id) => Path.Combine(_root, id.ToString());

    private static async Task<TemplateRecord?> ReadRecordAsync(string dir)
    {
        var metaPath = Path.Combine(dir, MetaFile);
        if (!File.Exists(metaPath))
            return null;

        var metaJson = await File.ReadAllTextAsync(metaPath);
        var meta = JsonSerializer.Deserialize<TemplateMeta>(metaJson, JsonOpts);
        if (meta is null)
            return null;

        var record = FromMeta(meta);

        // Read template source.
        var srcPath = Path.Combine(dir, SourceFile);
        record.Template = File.Exists(srcPath) ? await File.ReadAllTextAsync(srcPath) : string.Empty;

        // Read artefacts (every file except the two reserved ones).
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, MetaFile, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, SourceFile, StringComparison.OrdinalIgnoreCase))
                continue;

            var dotIndex = fileName.IndexOf('.');
            var name = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
            var ext = dotIndex >= 0 ? fileName[dotIndex..] : string.Empty;
            var content = await File.ReadAllTextAsync(file);
            record.Artefacts.Add(new TemplateArtefact { Name = name, Extension = ext, Content = content });
        }

        return record;
    }

    private static TemplateMeta ToMeta(TemplateRecord r) => new(
        r.Id, r.Name, r.Description, r.Mode,
        r.DataSchema, r.MockData, r.DefaultFileName,
        r.PageSettings, r.CreatedAt, r.UpdatedAt);

    private static TemplateRecord FromMeta(TemplateMeta m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        Mode = m.Mode,
        DataSchema = m.DataSchema,
        MockData = m.MockData,
        DefaultFileName = m.DefaultFileName,
        PageSettings = m.PageSettings ?? PageSettings.Default(),
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };

    // ── Internal DTO ─────────────────────────────────────────────────────────

    private record TemplateMeta(
        Guid Id, string Name, string? Description, TemplateMode Mode,
        string? DataSchema, object? MockData, string DefaultFileName,
        PageSettings? PageSettings, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
