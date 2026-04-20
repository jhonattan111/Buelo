namespace Buelo.Contracts;

public interface IGlobalArtefactStore
{
    Task<GlobalArtefact?> GetAsync(Guid id);
    Task<GlobalArtefact?> GetByNameAsync(string name, string extension);
    Task<IReadOnlyList<GlobalArtefact>> ListAsync(string? extensionFilter = null);
    Task<GlobalArtefact> SaveAsync(GlobalArtefact artefact);   // creates if Id == Guid.Empty
    Task<bool> DeleteAsync(Guid id);
}
