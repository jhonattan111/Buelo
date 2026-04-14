using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class TemplateModeDetectionTests
{
    // ── FullClass detection ───────────────────────────────────────────────────

    [Fact]
    public void ResolveTemplateMode_FullClassSource_ReturnsFullClass()
    {
        const string source = "public class Report : IReport { public byte[] GenerateReport(ReportContext ctx) { return new byte[0]; } }";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.FullClass);

        Assert.Equal(TemplateMode.FullClass, mode);
    }

    [Fact]
    public void ResolveTemplateMode_SourceWithIReport_ReturnsFullClass()
    {
        const string source = "// implements IReport\npublic class MyReport { }";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.FullClass);

        Assert.Equal(TemplateMode.FullClass, mode);
    }

    // ── Builder detection ─────────────────────────────────────────────────────

    [Fact]
    public void ResolveTemplateMode_BuilderExpression_ReturnsBuilder()
    {
        const string source = "Document.Create(c => c.Page(p => p.Content().Text(\"hi\"))).GeneratePdf()";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.FullClass);

        Assert.Equal(TemplateMode.Builder, mode);
    }

    // ── Sections detection ────────────────────────────────────────────────────

    [Fact]
    public void ResolveTemplateMode_StartsWithPageArrow_ReturnsSections()
    {
        const string source = "page => {\n    page.Size(PageSizes.A4);\n}\npage.Content().Text(\"hi\");";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.FullClass);

        Assert.Equal(TemplateMode.Sections, mode);
    }

    [Fact]
    public void ResolveTemplateMode_StartsWithPageContent_ReturnsSections()
    {
        const string source = "page.Content().Text(\"hi\");";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.FullClass);

        Assert.Equal(TemplateMode.Sections, mode);
    }

    [Fact]
    public void ResolveTemplateMode_HasImportDirective_ReturnsSections()
    {
        const string source = "@import header from \"h\"\npage.Content().Text(\"hi\");";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.FullClass);

        Assert.Equal(TemplateMode.Sections, mode);
    }

    // ── Explicit mode overrides ───────────────────────────────────────────────

    [Fact]
    public void ResolveTemplateMode_ExplicitBuilder_AlwaysReturnsBuilder()
    {
        // Even if it looks like FullClass, explicit Builder wins.
        const string source = "public class Report : IReport { }";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.Builder);

        Assert.Equal(TemplateMode.Builder, mode);
    }

    [Fact]
    public void ResolveTemplateMode_ExplicitSections_AlwaysReturnsSections()
    {
        const string source = "Document.Create(c => c.Page(p => p.Content().Text(\"hi\"))).GeneratePdf()";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.Sections);

        Assert.Equal(TemplateMode.Sections, mode);
    }

    [Fact]
    public void ResolveTemplateMode_ExplicitPartial_ReturnsPartial()
    {
        const string source = ".Text(\"Company header\").Bold();";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.Partial);

        Assert.Equal(TemplateMode.Partial, mode);
    }
}
