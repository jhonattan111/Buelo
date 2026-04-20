using Buelo.Contracts;

namespace Buelo.Engine.Renderers;

public class PdfRenderer : IOutputRenderer
{
    private readonly TemplateEngine _engine;

    public PdfRenderer(TemplateEngine engine)
    {
        _engine = engine;
    }

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public bool SupportsMode(TemplateMode mode) => mode == TemplateMode.BueloDsl;

    public Task<byte[]> RenderAsync(RendererInput input, CancellationToken cancellationToken = default)
    {
        if (!SupportsMode(input.Mode))
            throw new NotSupportedException("PDF rendering requires .buelo DSL mode.");

        return _engine.RenderAsync(input.Source, input.RawData!, input.Mode, input.PageSettings);
    }
}
