namespace Buelo.Engine.Renderers;

public class OutputRendererRegistry
{
    private readonly IReadOnlyDictionary<string, IOutputRenderer> _renderers;

    public OutputRendererRegistry(IEnumerable<IOutputRenderer> renderers)
    {
        _renderers = renderers.ToDictionary(
            r => r.Format,
            r => r,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> SupportedFormats => [.. _renderers.Keys];

    public IOutputRenderer GetRenderer(string format)
        => _renderers.TryGetValue(format, out var r) ? r
            : throw new InvalidOperationException($"No renderer registered for format '{format}'.");

    public IOutputRenderer? TryGetRenderer(string format)
        => _renderers.TryGetValue(format, out var r) ? r : null;
}
