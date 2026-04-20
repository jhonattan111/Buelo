using Buelo.Engine;
using Buelo.Engine.Renderers;

namespace Buelo.Tests.Engine;

public class OutputRendererRegistryTests
{
    private static OutputRendererRegistry CreateRegistry()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        return new OutputRendererRegistry([new PdfRenderer(engine), new ExcelRenderer()]);
    }

    [Fact]
    public void GetRenderer_Pdf_ReturnsPdfRenderer()
    {
        var registry = CreateRegistry();

        var renderer = registry.GetRenderer("pdf");

        Assert.IsType<PdfRenderer>(renderer);
        Assert.Equal("pdf", renderer.Format);
    }

    [Fact]
    public void GetRenderer_Excel_ReturnsExcelRenderer()
    {
        var registry = CreateRegistry();

        var renderer = registry.GetRenderer("excel");

        Assert.IsType<ExcelRenderer>(renderer);
        Assert.Equal("excel", renderer.Format);
    }

    [Fact]
    public void GetRenderer_Unknown_Throws()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.GetRenderer("word"));
    }

    [Fact]
    public void TryGetRenderer_Unknown_ReturnsNull()
    {
        var registry = CreateRegistry();

        var result = registry.TryGetRenderer("html");

        Assert.Null(result);
    }

    [Fact]
    public void SupportedFormats_ContainsBothFormats()
    {
        var registry = CreateRegistry();

        Assert.Contains("pdf", registry.SupportedFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("excel", registry.SupportedFormats, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRenderer_IsCaseInsensitive()
    {
        var registry = CreateRegistry();

        var renderer = registry.GetRenderer("PDF");
        Assert.Equal("pdf", renderer.Format);
    }
}
