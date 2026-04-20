using Buelo.Contracts;
using System.Collections.Concurrent;

namespace Buelo.Engine;

public class InMemoryGlobalArtefactStore : IGlobalArtefactStore
{
    private readonly ConcurrentDictionary<Guid, GlobalArtefact> _store = new();

    public Task<GlobalArtefact?> GetAsync(Guid id)
    {
        _store.TryGetValue(id, out var artefact);
        return Task.FromResult(artefact);
    }

    public Task<GlobalArtefact?> GetByNameAsync(string name, string extension)
    {
        var artefact = _store.Values.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.Extension, extension, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(artefact);
    }

    public Task<IReadOnlyList<GlobalArtefact>> ListAsync(string? extensionFilter = null)
    {
        IEnumerable<GlobalArtefact> query = _store.Values;
        if (extensionFilter is not null)
            query = query.Where(a => string.Equals(a.Extension, extensionFilter, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<IReadOnlyList<GlobalArtefact>>(query.ToList());
    }

    public Task<GlobalArtefact> SaveAsync(GlobalArtefact artefact)
    {
        var now = DateTimeOffset.UtcNow;

        if (artefact.Id == Guid.Empty)
        {
            artefact.Id = Guid.NewGuid();
            artefact.CreatedAt = now;
        }

        artefact.UpdatedAt = now;
        _store[artefact.Id] = artefact;

        return Task.FromResult(artefact);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var removed = _store.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
