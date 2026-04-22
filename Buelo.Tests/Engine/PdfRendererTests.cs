using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Renderers;
using QuestPDF;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace Buelo.Tests.Engine;

public class PdfRendererTests
{
    private const string ValidTemplate = """
        using QuestPDF.Fluent;
        using QuestPDF.Helpers;
        using QuestPDF.Infrastructure;
        public class HelloDocument : IDocument
        {
            private readonly dynamic _data;
            public HelloDocument(dynamic data) => _data = data;
            public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
            public void Compose(IDocumentContainer container)
            {
                container.Page(page => { page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre); page.Content().Text($"Hello {_data.name}"); });
            }
        }
        """;

    public PdfRendererTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static PdfRenderer CreateRenderer()
        => new(new TemplateEngine(new DefaultHelperRegistry()));

    private static JsonElement JsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task RenderAsync_FullClassMode_ReturnsPdfBytes()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = ValidTemplate,
            Mode = TemplateMode.FullClass,
            RawData = JsonData("World"),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_InvalidMode_Throws()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = ValidTemplate,
            Mode = (TemplateMode)999,
            RawData = JsonData("World"),
            PageSettings = PageSettings.Default()
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => renderer.RenderAsync(input));
    }

    [Fact]
    public void SupportsMode_FullClass_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        Assert.True(renderer.SupportsMode(TemplateMode.FullClass));
        Assert.False(renderer.SupportsMode((TemplateMode)999));
    }
}
