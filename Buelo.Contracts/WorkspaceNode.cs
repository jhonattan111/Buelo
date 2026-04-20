namespace Buelo.Contracts;

/// <summary>
/// Workspace tree node (folder or file).
/// </summary>
public class WorkspaceNode
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "file";
    public string Extension { get; set; } = string.Empty;
    public string Kind { get; set; } = "file";
    public IList<WorkspaceNode> Children { get; set; } = [];
}
