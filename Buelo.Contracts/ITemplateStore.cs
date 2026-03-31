namespace Buelo.Contracts;

/// <summary>
/// Persistence abstraction for <see cref="TemplateRecord"/> objects.
/// The default implementation is an in-memory store; swap it for a database-backed
/// implementation (e.g. Entity Framework Core + PostgreSQL) when persistence across
/// restarts is required.
/// </summary>
public interface ITemplateStore
{
    /// <summary>Returns the template with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<TemplateRecord?> GetAsync(Guid id);

    /// <summary>Returns all stored templates.</summary>
    Task<IEnumerable<TemplateRecord>> ListAsync();

    /// <summary>
    /// Persists <paramref name="template"/>.
    /// If <see cref="TemplateRecord.Id"/> is <see cref="Guid.Empty"/> a new GUID is assigned.
    /// </summary>
    Task<TemplateRecord> SaveAsync(TemplateRecord template);

    /// <summary>Removes the template with the given <paramref name="id"/>.</summary>
    /// <returns><c>true</c> if the template existed and was removed; <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(Guid id);
}
