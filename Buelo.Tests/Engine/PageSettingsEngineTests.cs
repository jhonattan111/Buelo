using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class PageSettingsTests
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
        var data = CreateJsonData("test");

        var pdf = await engine.RenderAsync(ValidTemplate, data, TemplateMode.FullClass);

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

        var data = CreateJsonData("test");

        var pdf = await engine.RenderAsync(ValidTemplate, data, TemplateMode.FullClass, customSettings);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task TemplateRecord_ShouldDefaultToA4Settings()
    {
        var template = new TemplateRecord
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Template = ValidTemplate,
            Mode = TemplateMode.FullClass
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
            Template = ValidTemplate,
            Mode = TemplateMode.FullClass,
            PageSettings = PageSettings.Default()
        };

        var customSettings = PageSettings.Letter();
        var data = CreateJsonData("test");

        var pdf = await engine.RenderTemplateAsync(template, data, customSettings);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void PageSettings_MergeSettings_RequestOverridesTemplate()
    {
        var templateSettings = new PageSettings
        {
            PageSize = "Letter",
            MarginHorizontal = 1.5f,
            MarginVertical = 1.5f,
            BackgroundColor = "#EEEEEE",
            ShowHeader = true,
            ShowFooter = true
        };

        var requestSettings = new PageSettings
        {
            PageSize = "A3",
            MarginHorizontal = 3.0f,
            MarginVertical = 3.0f,
            BackgroundColor = "#000000",
            ShowHeader = true,
            ShowFooter = false
        };

        var effective = TemplateEngine.MergeSettings(templateSettings, requestSettings);

        Assert.Equal("A3", effective.PageSize);
        Assert.Equal(3.0f, effective.MarginHorizontal);
        Assert.Equal("#000000", effective.BackgroundColor);
        Assert.True(effective.ShowHeader);
        Assert.False(effective.ShowFooter);
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
