namespace Buelo.Contracts;

public class GlobalArtefact
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;       // slug-safe, e.g. "colaborador"
    public string Extension { get; set; } = string.Empty;  // e.g. ".json", ".buelo", ".csx"
    public string Content { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IList<string> Tags { get; set; } = [];          // for filtering/search in UI
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
