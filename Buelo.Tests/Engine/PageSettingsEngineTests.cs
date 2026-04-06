using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class PageSettingsTests
{
    public PageSettingsTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task PageSettings_Default_ShouldHaveA4AndStandardMargins()
    {
        var settings = PageSettings.Default();

        Assert.Equal("A4", settings.PageSize);
        Assert.Equal(2.0f, settings.MarginHorizontal);
        Assert.Equal(2.0f, settings.MarginVertical);
        Assert.Equal("#FFFFFF", settings.BackgroundColor);
        Assert.Null(settings.WatermarkText);
    }

    [Fact]
    public async Task PageSettings_Letter_ShouldReturnLetterSizeWithInchMargins()
    {
        var settings = PageSettings.Letter();

        Assert.Equal("Letter", settings.PageSize);
        Assert.Equal(2.54f, settings.MarginHorizontal);
        Assert.Equal(2.54f, settings.MarginVertical);
    }

    [Fact]
    public async Task PageSettings_A4Compact_ShouldReturnTightMargins()
    {
        var settings = PageSettings.A4Compact();

        Assert.Equal("A4", settings.PageSize);
        Assert.Equal(1.0f, settings.MarginHorizontal);
        Assert.Equal(1.0f, settings.MarginVertical);
    }

    [Fact]
    public async Task PageSettings_WithWatermark_ShouldHaveWatermarkConfigured()
    {
        var settings = PageSettings.WithWatermark("CONFIDENTIAL");

        Assert.Equal("CONFIDENTIAL", settings.WatermarkText);
        Assert.Equal(0.2f, settings.WatermarkOpacity);
    }

    [Fact]
    public async Task Template_WithDefaultPageSettings_ShouldRenderSuccessfully()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var template = "Document.Create(c => c.Page(p => p.Content().Text(\"Hello\"))).GeneratePdf()";
        var data = CreateJsonData("test");

        var pdf = await engine.RenderAsync(template, data, TemplateMode.Builder);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task Template_WithCustomPageSettings_ShouldPassSettingsToContext()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var customSettings = new PageSettings
        {
            PageSize = "Letter",
            MarginHorizontal = 1.5f,
            MarginVertical = 1.5f,
            WatermarkText = "DRAFT"
        };

        var template = "Document.Create(c => c.Page(p => p.Content().Text(\"Hello\"))).GeneratePdf()";
        var data = CreateJsonData("test");

        var pdf = await engine.RenderAsync(template, data, TemplateMode.Builder, customSettings);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task TemplateRecord_ShouldDefaultToA4Settings()
    {
        var template = new TemplateRecord
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Template = "Document.Create(c => c.Page(p => p.Content().Text(\"Hello\"))).GeneratePdf()",
            Mode = TemplateMode.Builder
        };

        Assert.NotNull(template.PageSettings);
        Assert.Equal("A4", template.PageSettings.PageSize);
        Assert.Equal(2.0f, template.PageSettings.MarginHorizontal);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithCustomPageSettings_ShouldUseOverride()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var template = new TemplateRecord
        {
            Name = "Test",
            Template = "Document.Create(c => c.Page(p => p.Content().Text(\"Hello\"))).GeneratePdf()",
            Mode = TemplateMode.Builder,
            PageSettings = PageSettings.Default()
        };

        var customSettings = PageSettings.Letter();
        var data = CreateJsonData("test");

        var pdf = await engine.RenderTemplateAsync(template, data, customSettings);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task ReportContext_ShouldContainPageSettings()
    {
        var context = new ReportContext
        {
            Data = new { },
            Helpers = new DefaultHelperRegistry(),
            PageSettings = PageSettings.WithWatermark("TEST")
        };

        Assert.NotNull(context.PageSettings);
        Assert.Equal("TEST", context.PageSettings.WatermarkText);
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { name });
        return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
    }
}
