using Buelo.Contracts;
using System.Collections.Concurrent;

namespace Buelo.Engine;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ITemplateStore"/>.
/// Templates are lost when the process restarts.
/// To persist templates across restarts, replace this with a database-backed
/// implementation (see the README for an Entity Framework Core + PostgreSQL guide).
/// </summary>
public class InMemoryTemplateStore : ITemplateStore
{
    private readonly ConcurrentDictionary<Guid, TemplateRecord> _store = new();

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

        template.UpdatedAt = DateTimeOffset.UtcNow;
        _store[template.Id] = template;
        return Task.FromResult(template);
    }

    public Task<bool> DeleteAsync(Guid id)
        => Task.FromResult(_store.TryRemove(id, out _));
}
