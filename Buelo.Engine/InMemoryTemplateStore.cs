using Buelo.Contracts;
using System.Collections.Concurrent;

namespace Buelo.Engine;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ITemplateStore"/>.
/// Templates are lost when the process restarts.
/// Keeps up to <see cref="MaxVersionsPerTemplate"/> version snapshots per template.
/// </summary>
public class InMemoryTemplateStore : ITemplateStore
{
    /// <summary>Maximum number of versions retained per template.</summary>
    public const int MaxVersionsPerTemplate = 20;

    private readonly ConcurrentDictionary<Guid, TemplateRecord> _store = new();
    private readonly ConcurrentDictionary<Guid, List<TemplateVersion>> _versions = new();

    public Task<TemplateRecord?> GetAsync(Guid id)
    {
        _store.TryGetValue(id, out var template);
        return Task.FromResult(template);
    }

    public Task<IEnumerable<TemplateRecord>> ListAsync()
        => Task.FromResult<IEnumerable<TemplateRecord>>(_store.Values.ToList());

    public Task<TemplateRecord> SaveAsync(TemplateRecord template)
    {
        if (template.Id == Guid.Empty)
        {
            template.Id = Guid.NewGuid();
            template.CreatedAt = DateTimeOffset.UtcNow;
        }
        else if (_store.TryGetValue(template.Id, out var existing))
        {
            // Snapshot the stored copy (which has the old content) before overwriting.
            var history = _versions.GetOrAdd(template.Id, _ => []);
            lock (history)
            {
                history.Add(new TemplateVersion
                {
                    Version = history.Count + 1,
                    Template = existing.Template,
                    Artefacts = existing.Artefacts.Select(a => new TemplateArtefact
                    {
                        Name = a.Name,
                        Extension = a.Extension,
                        Content = a.Content
                    }).ToList(),
                    SavedAt = existing.UpdatedAt
                });

                // Keep only the last MaxVersionsPerTemplate entries.
                while (history.Count > MaxVersionsPerTemplate)
                    history.RemoveAt(0);
            }
        }

        template.UpdatedAt = DateTimeOffset.UtcNow;

        // Store a defensive copy so later mutations by the caller don't corrupt the store.
        _store[template.Id] = DeepCopy(template);
        return Task.FromResult(template);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        _versions.TryRemove(id, out _);
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    public Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(Guid id)
    {
        if (_versions.TryGetValue(id, out var history))
        {
            lock (history)
                return Task.FromResult<IReadOnlyList<TemplateVersion>>(history.ToList());
        }
        return Task.FromResult<IReadOnlyList<TemplateVersion>>([]);
    }

    public Task<TemplateVersion?> GetVersionAsync(Guid id, int version)
    {
        if (_versions.TryGetValue(id, out var history))
        {
            lock (history)
            {
                var found = history.FirstOrDefault(v => v.Version == version);
                return Task.FromResult(found);
            }
        }
        return Task.FromResult<TemplateVersion?>(null);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static TemplateRecord DeepCopy(TemplateRecord src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        Description = src.Description,
        Template = src.Template,
        Mode = src.Mode,
        DataSchema = src.DataSchema,
        MockData = src.MockData,
        DefaultFileName = src.DefaultFileName,
        PageSettings = src.PageSettings,
        CreatedAt = src.CreatedAt,
        UpdatedAt = src.UpdatedAt,
        Artefacts = src.Artefacts.Select(a => new TemplateArtefact
        {
            Name = a.Name,
            Extension = a.Extension,
            Content = a.Content
        }).ToList()
    };
}
