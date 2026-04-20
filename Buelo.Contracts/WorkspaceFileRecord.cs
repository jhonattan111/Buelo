namespace Buelo.Contracts;

public class WorkspaceFileRecord
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; set; }
}
