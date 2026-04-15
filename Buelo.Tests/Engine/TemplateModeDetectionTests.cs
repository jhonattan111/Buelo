using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class TemplateModeDetectionTests
{
    // ── Sections detection ────────────────────────────────────────────────────

    [Fact]
    public void ResolveTemplateMode_DefaultMode_ReturnsSections()
    {
        const string source = "page.Content().Text(\"hi\");";

        var mode = TemplateEngine.ResolveTemplateMode(source, TemplateMode.Sections);

        Assert.Equal(TemplateMode.Sections, mode);
    }

    [Fact]
    public void ResolveTemplateMode_ExplicitSections_AlwaysReturnsSections()
    {
        const string source = "page.Content().Text(\"hi\");";

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
