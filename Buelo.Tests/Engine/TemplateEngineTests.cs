using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class TemplateEngineTests
{
    private const string BueloTemplate = """
        report title:
          text: "Hello {{ data.name }}"
        """;

    public TemplateEngineTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_BueloTemplate_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var pdf = await engine.RenderAsync(BueloTemplate, CreateJsonData("World"), TemplateMode.BueloDsl);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderTemplateAsync_Record_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var template = new TemplateRecord
        {
            Name = "BueloDsl",
            Template = BueloTemplate,
            Mode = TemplateMode.BueloDsl
        };

        var pdf = await engine.RenderTemplateAsync(template, CreateJsonData("World"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void ValidateAsync_InvalidBueloDsl_ReturnsErrors()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var result = engine.ValidateAsync("report title:\n  text: \"{{ unclosed\"", TemplateMode.BueloDsl).Result;

        Assert.False(result.Valid);
        Assert.NotEmpty(result.Errors);
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
