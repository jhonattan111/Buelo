using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class TemplateEngineTests
{
    private const string FullClassTemplate = "public class Report : IReport { public byte[] GenerateReport(ReportContext ctx) { var data = ctx.Data; return Document.Create(c => c.Page(p => p.Content().Text((string)data.name))).GeneratePdf(); } }";
    private const string BuilderTemplate = "Document.Create(c => c.Page(p => p.Content().Text((string)data.name))).GeneratePdf()";

    public TemplateEngineTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_FullClassTemplate_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var pdf = await engine.RenderAsync(FullClassTemplate, CreateJsonData("World"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_BuilderTemplateWithoutMode_ShouldAutoDetectAndGeneratePdf()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var pdf = await engine.RenderAsync(BuilderTemplate, CreateJsonData("World"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderTemplateAsync_BuilderModeRecord_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var template = new TemplateRecord
        {
            Name = "Builder",
            Template = BuilderTemplate,
            Mode = TemplateMode.Builder
        };

        var pdf = await engine.RenderTemplateAsync(template, CreateJsonData("World"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void ConvertToDynamic_JsonElementObject_ShouldKeepStructure()
    {
        var json = """
                   {
                     "name": "World",
                     "count": 2,
                     "active": true,
                     "nested": {
                       "city": "Sao Paulo"
                     }
                   }
                   """;

        var element = JsonSerializer.Deserialize<JsonElement>(json);

        dynamic result = TemplateEngine.ConvertToDynamic(element);

        Assert.Equal("World", (string)result.name);
        Assert.Equal(2L, (long)result.count);
        Assert.True((bool)result.active);
        Assert.Equal("Sao Paulo", (string)result.nested.city);
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
