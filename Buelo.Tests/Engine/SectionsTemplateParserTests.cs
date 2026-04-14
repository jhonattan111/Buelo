using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class SectionsTemplateParserTests
{
    // ── ParseImports ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseImports_SingleHeaderImport_ReturnsSingleDirective()
    {
        var source = """
            @import header from "shared-header"
            page.Content().Text("hello");
            """;

        var imports = SectionsTemplateParser.ParseImports(source);

        Assert.Single(imports);
        Assert.Equal(SectionSlot.Header, imports[0].Slot);
        Assert.Equal("shared-header", imports[0].Target);
    }

    [Fact]
    public void ParseImports_GuidTarget_ParsesCorrectly()
    {
        const string id = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
        var source = $"""
            @import footer from "{id}"
            page.Content().Text("hello");
            """;

        var imports = SectionsTemplateParser.ParseImports(source);

        Assert.Single(imports);
        Assert.Equal(SectionSlot.Footer, imports[0].Slot);
        Assert.Equal(id, imports[0].Target);
    }

    [Fact]
    public void ParseImports_NoImports_ReturnsEmpty()
    {
        var source = "page.Content().Text(\"hello\");";

        var imports = SectionsTemplateParser.ParseImports(source);

        Assert.Empty(imports);
    }

    [Fact]
    public void ParseImports_AllThreeSlots_ReturnsAllDirectives()
    {
        var source = """
            @import header from "company-header"
            @import footer from "standard-footer"
            @import content from "body-fragment"
            """;

        var imports = SectionsTemplateParser.ParseImports(source);

        Assert.Equal(3, imports.Count);
        Assert.Contains(imports, i => i.Slot == SectionSlot.Header && i.Target == "company-header");
        Assert.Contains(imports, i => i.Slot == SectionSlot.Footer && i.Target == "standard-footer");
        Assert.Contains(imports, i => i.Slot == SectionSlot.Content && i.Target == "body-fragment");
    }

    [Fact]
    public void ParseImports_IsCaseInsensitiveForSlot()
    {
        var source = """
            @import HEADER from "h"
            @import Footer from "f"
            """;

        var imports = SectionsTemplateParser.ParseImports(source);

        Assert.Equal(2, imports.Count);
        Assert.Equal(SectionSlot.Header, imports[0].Slot);
        Assert.Equal(SectionSlot.Footer, imports[1].Slot);
    }

    // ── StripDirectives ───────────────────────────────────────────────────────

    [Fact]
    public void StripDirectives_RemovesImportLines()
    {
        var source = "@import header from \"company-header\"\npage.Content().Text(\"hello\");";

        var result = SectionsTemplateParser.StripDirectives(source);

        Assert.DoesNotContain("@import", result);
        Assert.Contains("page.Content", result);
    }

    [Fact]
    public void StripDirectives_NoDirectives_ReturnsSameSource()
    {
        var source = "page.Content().Text(\"hello\");";

        var result = SectionsTemplateParser.StripDirectives(source);

        Assert.Equal(source, result);
    }

    // ── ParsePageConfig ───────────────────────────────────────────────────────

    [Fact]
    public void ParsePageConfig_ValidBlock_ReturnsBodyStatements()
    {
        var source = """
            page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
            }
            page.Content().Text("hello");
            """;

        var config = SectionsTemplateParser.ParsePageConfig(source);

        Assert.NotNull(config);
        Assert.Contains("page.Size", config);
        Assert.Contains("page.Margin", config);
    }

    [Fact]
    public void ParsePageConfig_NoBlock_ReturnsNull()
    {
        var source = "page.Content().Text(\"hello\");";

        var config = SectionsTemplateParser.ParsePageConfig(source);

        Assert.Null(config);
    }

    [Fact]
    public void ParsePageConfig_BlockWithNestedBraces_ReturnsFullBody()
    {
        var source = """
            page => {
                page.DefaultTextStyle(x => x.FontSize(12));
            }
            page.Content().Text("hi");
            """;

        var config = SectionsTemplateParser.ParsePageConfig(source);

        Assert.NotNull(config);
        Assert.Contains("DefaultTextStyle", config);
    }

    // ── ParseSection ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseSection_SimpleHeader_ReturnsFullStatement()
    {
        var source = "page.Header().Text(\"Hello\").Bold();";

        var section = SectionsTemplateParser.ParseSection(source, SectionSlot.Header);

        Assert.NotNull(section);
        Assert.StartsWith("page.Header(", section);
        Assert.EndsWith(";", section);
    }

    [Fact]
    public void ParseSection_ContentWithNestedLambda_ReturnsCorrectStatement()
    {
        var source = "page.Content().Column(x => { x.Item().Text(\"hi\"); });";

        var section = SectionsTemplateParser.ParseSection(source, SectionSlot.Content);

        Assert.NotNull(section);
        Assert.Contains("Column", section);
        Assert.EndsWith(";", section);
    }

    [Fact]
    public void ParseSection_MultilineContent_ReturnsFullStatement()
    {
        var source = """
            page.Content()
                .PaddingVertical(1, Unit.Centimetre)
                .Column(x => {
                    x.Spacing(20);
                    x.Item().Text(Placeholders.LoremIpsum());
                });
            """;

        var section = SectionsTemplateParser.ParseSection(source, SectionSlot.Content);

        Assert.NotNull(section);
        Assert.Contains("PaddingVertical", section);
        Assert.Contains("Column", section);
        Assert.EndsWith(";", section);
    }

    [Fact]
    public void ParseSection_AbsentSlot_ReturnsNull()
    {
        var source = "page.Content().Text(\"hello\");";

        Assert.Null(SectionsTemplateParser.ParseSection(source, SectionSlot.Header));
        Assert.Null(SectionsTemplateParser.ParseSection(source, SectionSlot.Footer));
    }

    [Fact]
    public void ParseSection_FooterPresent_ReturnsFooterStatement()
    {
        var source = "page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); });";

        var section = SectionsTemplateParser.ParseSection(source, SectionSlot.Footer);

        Assert.NotNull(section);
        Assert.Contains("page.Footer(", section);
        Assert.Contains("AlignCenter", section);
    }

    // ── IsSectionsTemplate ────────────────────────────────────────────────────

    [Fact]
    public void IsSectionsTemplate_WithImport_ReturnsTrue()
    {
        var source = "@import header from \"h\"\npage.Content().Text(\"hi\");";

        Assert.True(SectionsTemplateParser.IsSectionsTemplate(source));
    }

    [Fact]
    public void IsSectionsTemplate_StartsWithPageArrow_ReturnsTrue()
    {
        var source = "page => {\n    page.Size(PageSizes.A4);\n}\npage.Content().Text(\"hi\");";

        Assert.True(SectionsTemplateParser.IsSectionsTemplate(source));
    }

    [Fact]
    public void IsSectionsTemplate_StartsWithPageContent_ReturnsTrue()
    {
        var source = "page.Content().Text(\"hi\");";

        Assert.True(SectionsTemplateParser.IsSectionsTemplate(source));
    }

    [Fact]
    public void IsSectionsTemplate_BuilderTemplate_ReturnsFalse()
    {
        var source = "Document.Create(c => c.Page(p => p.Content().Text(\"hi\"))).GeneratePdf()";

        Assert.False(SectionsTemplateParser.IsSectionsTemplate(source));
    }

    [Fact]
    public void IsSectionsTemplate_Empty_ReturnsFalse()
    {
        Assert.False(SectionsTemplateParser.IsSectionsTemplate(string.Empty));
        Assert.False(SectionsTemplateParser.IsSectionsTemplate("   "));
    }
}
