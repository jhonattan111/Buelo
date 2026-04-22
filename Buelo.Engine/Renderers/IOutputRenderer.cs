using Buelo.Contracts;

namespace Buelo.Engine.Renderers;

public interface IOutputRenderer
{
    /// <summary>Format identifier used in the ?format= query parameter.</summary>
    string Format { get; }

    /// <summary>MIME type for the HTTP response Content-Type header.</summary>
    string ContentType { get; }

    /// <summary>File extension for Content-Disposition attachment filename (e.g. ".pdf").</summary>
    string FileExtension { get; }

    /// <summary>Returns true if this renderer supports the given template mode.</summary>
    bool SupportsMode(TemplateMode mode);

    /// <summary>Renders the report and returns raw bytes.</summary>
    Task<byte[]> RenderAsync(RendererInput input, CancellationToken cancellationToken = default);
}
