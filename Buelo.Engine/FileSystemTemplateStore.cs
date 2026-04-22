using Buelo.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buelo.Engine;

/// <summary>
/// File-system backed implementation of <see cref="ITemplateStore"/>.
/// <para>
/// Each template is stored as a single self-contained JSON file:
/// <code>
/// {root}/{id}/
///   template.record.json   — full record: id, name, template source, artefacts (all embedded)
///   versions/
///     1.snapshot.json      — version snapshot
///     2.snapshot.json
///     ...
/// </code>
/// </para>
/// <para>
/// Storing everything inside the record JSON means no loose <c>.cs</c> files on disk
/// (avoiding MSBuild/dotnet-watch picking them up), and migrating to a database only
/// requires reading the JSON and inserting one row per template.
/// </para>
/// <para>Opt into this store via <c>builder.Services.AddBueloFileSystemStore()</c>.</para>
/// </summary>
public class FileSystemTemplateStore : ITemplateStore
{
    private const string RecordFile = "template.record.json";

    // Legacy file names kept only for backward-compat reads during migration.
    private const string LegacySourceFile = "template.report.cs";
    private const string VersionsDir = "versions";

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
            // Skip the versions sub-directory that might accidentally be enumerated in edge cases.
            var dirName = Path.GetFileName(dir);
            if (string.Equals(dirName, VersionsDir, StringComparison.OrdinalIgnoreCase))
                continue;

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
        else
        {
            // Snapshot the current state before overwriting.
            var existing = await GetAsync(template.Id);
            if (existing is not null)
                await WriteVersionSnapshotAsync(template.Id, existing);
        }

        template.UpdatedAt = DateTimeOffset.UtcNow;

        var dir = TemplateDir(template.Id);
        Directory.CreateDirectory(dir);

        // Write the full record (metadata + template source + artefacts) as a single JSON file.
        // No separate .cs or artefact files are created — everything lives in this one record.
        var json = JsonSerializer.Serialize(template, JsonOpts);
        await File.WriteAllTextAsync(Path.Combine(dir, RecordFile), json);

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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(Guid id)
    {
        var versionsDir = VersionsDirPath(id);
        if (!Directory.Exists(versionsDir))
            return [];

        var results = new List<TemplateVersion>();
        foreach (var file in Directory.EnumerateFiles(versionsDir, "*.snapshot.json"))
        {
            var json = await File.ReadAllTextAsync(file);
            var v = JsonSerializer.Deserialize<TemplateVersion>(json, JsonOpts);
            if (v is not null)
                results.Add(v);
        }
        return results.OrderBy(v => v.Version).ToList();
    }

    /// <inheritdoc/>
    public async Task<TemplateVersion?> GetVersionAsync(Guid id, int version)
    {
        var file = VersionFilePath(id, version);
        if (!File.Exists(file))
            return null;

        var json = await File.ReadAllTextAsync(file);
        return JsonSerializer.Deserialize<TemplateVersion>(json, JsonOpts);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string TemplateDir(Guid id) => Path.Combine(_root, id.ToString());
    private string VersionsDirPath(Guid id) => Path.Combine(TemplateDir(id), VersionsDir);
    private string VersionFilePath(Guid id, int version) => Path.Combine(VersionsDirPath(id), $"{version}.snapshot.json");

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith('/'))
            normalized = normalized[1..];

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException($"Invalid relative path '{path}'.");

        return string.Join('/', segments);
    }

    private async Task WriteVersionSnapshotAsync(Guid id, TemplateRecord existing)
    {
        var versionsDir = VersionsDirPath(id);
        Directory.CreateDirectory(versionsDir);

        var nextVersion = Directory.EnumerateFiles(versionsDir, "*.snapshot.json").Count() + 1;

        var snapshot = new TemplateVersion
        {
            Version = nextVersion,
            Template = existing.Template,
            Artefacts = existing.Artefacts.Select(a => new TemplateArtefact
            {
                Path = a.Path,
                Name = a.Name,
                Extension = a.Extension,
                Content = a.Content
            }).ToList(),
            SavedAt = existing.UpdatedAt
        };

        var json = JsonSerializer.Serialize(snapshot, JsonOpts);
        await File.WriteAllTextAsync(VersionFilePath(id, nextVersion), json);
    }

    private static async Task<TemplateRecord?> ReadRecordAsync(string dir)
    {
        var recordPath = Path.Combine(dir, RecordFile);
        if (!File.Exists(recordPath))
            return null;

        var json = await File.ReadAllTextAsync(recordPath);

        // Try to deserialize as the full self-contained TemplateRecord (new format).
        // Fall back gracefully to the legacy split-file layout for backward compat.
        var record = JsonSerializer.Deserialize<TemplateRecord>(json, JsonOpts);
        if (record is null)
            return null;

        // Backward-compat: template source was stored in a separate .cs file in the old format.
        if (string.IsNullOrEmpty(record.Template))
        {
            var srcPath = Path.Combine(dir, LegacySourceFile);
            if (File.Exists(srcPath))
                record.Template = await File.ReadAllTextAsync(srcPath);
        }

        // Backward-compat: artefacts were separate files on disk in the old format.
        if (record.Artefacts.Count == 0)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var rel = NormalizeRelativePath(Path.GetRelativePath(dir, file));
                if (rel.StartsWith($"{VersionsDir}/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(rel, RecordFile, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(rel, LegacySourceFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(rel);
                var dotIndex = fileName.IndexOf('.');
                var name = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
                var ext = dotIndex >= 0 ? fileName[dotIndex..] : string.Empty;
                var content = await File.ReadAllTextAsync(file);
                record.Artefacts.Add(new TemplateArtefact
                {
                    Path = rel,
                    Name = name,
                    Extension = ext,
                    Content = content
                });
            }
        }

        return record;
    }
}