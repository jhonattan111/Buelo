using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.BueloDsl;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class BueloDslEngineTests
{
    public BueloDslEngineTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_ValidBueloDsl_ReturnsPdfBytes()
    {
        var engine = new BueloDslEngine(new DefaultHelperRegistry());
        const string source = """
            report title:
              text: "Hello World"
            """;
        var context = new ReportContext
        {
            Data = CreateJsonData("World"),
            Helpers = new DefaultHelperRegistry(),
            PageSettings = PageSettings.Default()
        };

        var pdf = await engine.RenderAsync(source, context);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderAsync_WithPageHeaderAndFooter_ReturnsPdfBytes()
    {
        var engine = new BueloDslEngine(new DefaultHelperRegistry());
        const string source = """
            page header:
              text: "My Header"
            report title:
              text: "Content"
            page footer:
              text: "My Footer"
            """;
        var context = new ReportContext
        {
            Data = CreateJsonData("Test"),
            Helpers = new DefaultHelperRegistry(),
            PageSettings = PageSettings.Default()
        };

        var pdf = await engine.RenderAsync(source, context);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void Validate_ValidSource_ReturnsNoErrors()
    {
        var engine = new BueloDslEngine(new DefaultHelperRegistry());

        var (valid, errors) = engine.Validate("""
            report title:
              text: "Hello"
            """);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingClosingBrace_ReturnsError()
    {
        var engine = new BueloDslEngine(new DefaultHelperRegistry());

        var (valid, errors) = engine.Validate("""
            report title:
              text: "{{ unclosed"
            """);

        Assert.False(valid);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Severity == BueloDslErrorSeverity.Error);
    }

    [Fact]
    public async Task RenderAsync_ParseErrorsThrowInvalidOperation()
    {
        var engine = new BueloDslEngine(new DefaultHelperRegistry());

        // Simulate a source with an unbalanced expression
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RenderAsync(
                "report title:\n  text: \"{{ unclosed\"\n",
                new ReportContext
                {
                    Data = CreateJsonData("x"),
                    Helpers = new DefaultHelperRegistry(),
                    PageSettings = PageSettings.Default()
                }));
    }

    [Fact]
    public async Task TemplateEngine_BueloDslMode_RendersSuccessfully()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        const string source = """
            report title:
              text: "Auto-detected"
            """;

        var pdf = await engine.RenderAsync(source, CreateJsonData("test"), TemplateMode.BueloDsl);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task TemplateEngine_BueloDslMode_ExplicitMode_ReturnsPdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        const string source = """
            report title:
              text: "Explicit BueloDsl Mode"
            """;

        var pdf = await engine.RenderAsync(source, CreateJsonData("test"), TemplateMode.BueloDsl);

        Assert.NotEmpty(pdf);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JsonElement CreateJsonData(string name)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { name });
        return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
    }
}
