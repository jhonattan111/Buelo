using Buelo.Contracts;

namespace Buelo.Engine;

public class InMemoryBueloProjectStore : IBueloProjectStore
{
    private BueloProject _project = new();

    public Task<BueloProject> GetAsync() => Task.FromResult(_project);

    public Task<BueloProject> SaveAsync(BueloProject project)
    {
        project.UpdatedAt = DateTimeOffset.UtcNow;
        if (project.CreatedAt == default)
            project.CreatedAt = project.UpdatedAt;
        _project = project;
        return Task.FromResult(_project);
    }
}
