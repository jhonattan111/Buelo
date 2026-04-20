using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Renderers;
using QuestPDF;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace Buelo.Tests.Engine;

public class PdfRendererTests
{
    private const string SectionsTemplate = "page.Content().Text((string)data.name);";
    private const string BueloDslSource = """
        report title:
          text: Hello World
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
    public async Task RenderAsync_SectionsMode_ReturnsPdfBytes()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = SectionsTemplate,
            Mode = TemplateMode.Sections,
            RawData = JsonData("World"),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_BueloDslMode_ReturnsPdfBytes()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = BueloDslSource,
            Mode = TemplateMode.BueloDsl,
            RawData = JsonData("World"),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void SupportsMode_AllModes_ReturnsTrue()
    {
        var renderer = CreateRenderer();
        Assert.True(renderer.SupportsMode(TemplateMode.Sections));
        Assert.True(renderer.SupportsMode(TemplateMode.BueloDsl));
        Assert.True(renderer.SupportsMode(TemplateMode.Partial));
    }
}
