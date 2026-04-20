namespace Buelo.Contracts;

public interface IBueloProjectStore
{
    Task<BueloProject> GetAsync();
    Task<BueloProject> SaveAsync(BueloProject project);
}
