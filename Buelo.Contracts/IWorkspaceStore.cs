namespace Buelo.Contracts;

/// <summary>
/// File-system oriented workspace abstraction used by the editor and render pipeline.
/// All paths are workspace-relative and use '/' as separator.
/// </summary>
public interface IWorkspaceStore
{
    Task<IReadOnlyList<WorkspaceNode>> GetTreeAsync();

    Task<IReadOnlyList<WorkspaceFileRecord>> ListFilesAsync(string? extension = null);

    Task<WorkspaceFileRecord?> GetFileAsync(string path);

    Task<WorkspaceFileRecord> CreateFileAsync(string path, string content = "", bool overwrite = false);

    Task<WorkspaceFileRecord> UpdateFileAsync(string path, string content, bool createIfMissing = false);

    Task CreateFolderAsync(string path);

    Task MoveAsync(string path, string destinationPath, bool overwrite = false);

    Task RenameAsync(string path, string newName, bool overwrite = false);

    Task DeleteAsync(string path, bool recursive = true);

    Task<bool> ExistsAsync(string path);
}
