using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class TemplateEngineTests
{
    private const string SectionsTemplate = "page.Content().Text((string)data.name);";

    public TemplateEngineTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_SectionsTemplate_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var pdf = await engine.RenderAsync(SectionsTemplate, CreateJsonData("World"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderTemplateAsync_SectionsModeRecord_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var template = new TemplateRecord
        {
            Name = "Sections",
            Template = SectionsTemplate,
            Mode = TemplateMode.Sections
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

    // ── Sections mode ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_SectionsModeContentOnly_ShouldGeneratePdf()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        const string template = "page.Content().Text((string)data.name);";

        var pdf = await engine.RenderAsync(template, CreateJsonData("Sections"), TemplateMode.Sections);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_SectionsModeAutoDetected_ShouldGeneratePdf()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        // Starts with page.Content( → auto-detected as Sections
        const string template = "page.Content().Text((string)data.name);";

        var pdf = await engine.RenderAsync(template, CreateJsonData("AutoDetect"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_SectionsModeWithAllBlocks_ShouldGeneratePdf()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        const string template = """
            page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
            }
            page.Header().Text((string)data.name).Bold();
            page.Content().Text("Body text");
            page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
            """;

        var pdf = await engine.RenderAsync(template, CreateJsonData("AllBlocks"), TemplateMode.Sections);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_SectionsModeWithImport_InlineFallbackWhenNotFound()
    {
        // Import target does not exist in store → engine falls back to inline block.
        var store = new InMemoryTemplateStore();
        var engine = new TemplateEngine(new DefaultHelperRegistry(), store);
        const string template = """
            @import header from "non-existent-partial"
            page.Header().Text("Fallback Header");
            page.Content().Text("Body");
            """;

        var pdf = await engine.RenderAsync(template, CreateJsonData("FallbackTest"), TemplateMode.Sections);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_SectionsModeImportByName_UsesPartialBody()
    {
        var store = new InMemoryTemplateStore();
        await store.SaveAsync(new TemplateRecord
        {
            Name = "company-header",
            Mode = TemplateMode.Partial,
            Template = ".Text(\"Acme Corp\").Bold().FontSize(18);"
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry(), store);
        const string template = """
            @import header from "company-header"
            page.Content().Text("Body");
            """;

        var pdf = await engine.RenderAsync(template, CreateJsonData("ImportTest"), TemplateMode.Sections);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_SectionsModeImportByGuid_UsesPartialBody()
    {
        var store = new InMemoryTemplateStore();
        var partial = await store.SaveAsync(new TemplateRecord
        {
            Name = "footer-partial",
            Mode = TemplateMode.Partial,
            Template = ".AlignCenter().Text(x => { x.Span(\"Page \"); x.CurrentPageNumber(); });"
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry(), store);
        var template = $"""
            @import footer from "{partial.Id}"
            page.Content().Text("Body");
            """;

        var pdf = await engine.RenderAsync(template, CreateJsonData("GuidImport"), TemplateMode.Sections);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_SectionsModeImportOverridesInlineBlock()
    {
        // Both @import header AND inline page.Header() present — import wins (no compile error expected).
        var store = new InMemoryTemplateStore();
        await store.SaveAsync(new TemplateRecord
        {
            Name = "shared-header",
            Mode = TemplateMode.Partial,
            Template = ".Text(\"Imported Header\").Bold();"
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry(), store);
        const string template = """
            @import header from "shared-header"
            page.Header().Text("Inline Header — should be ignored");
            page.Content().Text("Body");
            """;

        var pdf = await engine.RenderAsync(template, CreateJsonData("OverrideTest"), TemplateMode.Sections);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderTemplateAsync_SectionsModeRecord_ShouldGeneratePdf()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var record = new TemplateRecord
        {
            Name = "Sections Record",
            Mode = TemplateMode.Sections,
            Template = "page.Content().Text((string)data.name);"
        };

        var pdf = await engine.RenderTemplateAsync(record, CreateJsonData("RecordTest"));

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_PartialMode_ShouldThrowInvalidOperation()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RenderAsync(".Text(\"Fragment\");", CreateJsonData("Partial"), TemplateMode.Partial));

        Assert.Contains("cannot be rendered directly", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
