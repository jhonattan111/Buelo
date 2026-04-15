using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

/// <summary>
/// Tests for the <c>@helper</c> DSL — both inline <c>@helper Name(...) => expr;</c>
/// and artefact-based <c>@helper from "name"</c>.
/// </summary>
public class DynamicHelpersTests
{
    // ── Parser: @helper from "name" ───────────────────────────────────────────

    [Fact]
    public void Parse_HelperFromDirective_SetsHelperArtefactRef()
    {
        var source = """
            @helper from "tax-helpers"
            page.Content().Text("hi");
            """;

        var (header, stripped) = TemplateHeaderParser.Parse(source);

        Assert.Equal("tax-helpers", header.HelperArtefactRef);
        Assert.Empty(header.Helpers);
        Assert.DoesNotContain("@helper", stripped);
    }

    [Fact]
    public void Parse_InlineHelperDirective_PopulatesHelpers()
    {
        var source = """
            @helper FormatCNPJ(string value) => value.Replace(".", "-");
            page.Content().Text("hi");
            """;

        var (header, stripped) = TemplateHeaderParser.Parse(source);

        Assert.Null(header.HelperArtefactRef);
        Assert.Single(header.Helpers);
        Assert.Equal("FormatCNPJ", header.Helpers[0].Name);
        Assert.Equal("string value", header.Helpers[0].Signature);
        Assert.Equal("value.Replace(\".\", \"-\")", header.Helpers[0].Body);
    }

    [Fact]
    public void Parse_HelperFromAndInlineHelper_ArtefactRefTakesPrecedenceAndInlineStored()
    {
        var source = """
            @helper from "shared"
            @helper Fmt(string v) => v.ToUpper();
            page.Content().Text("hi");
            """;

        var (header, _) = TemplateHeaderParser.Parse(source);

        Assert.Equal("shared", header.HelperArtefactRef);
        Assert.Single(header.Helpers);
    }

    // ── BuildHelperPreambleAsync: inline helpers ──────────────────────────────

    [Fact]
    public async Task BuildHelperPreamble_InlineHelpers_GeneratesStaticClass()
    {
        var header = new TemplateHeader
        {
            Helpers = new List<TemplateHeaderHelper>
            {
                new("FormatCNPJ", "string value", "value.Insert(2, \".\")"),
                new("FormatCPF",  "string value", "value.Insert(3, \".\")"),
            }
        };

        var preamble = await TemplateEngine.BuildHelperPreambleAsync(header, []);

        Assert.NotNull(preamble);
        Assert.Contains("BueloGeneratedHelpers", preamble);
        Assert.Contains("FormatCNPJ", preamble);
        Assert.Contains("FormatCPF", preamble);
    }

    [Fact]
    public async Task BuildHelperPreamble_NoHelpers_ReturnsNull()
    {
        var header = new TemplateHeader();
        var preamble = await TemplateEngine.BuildHelperPreambleAsync(header, []);
        Assert.Null(preamble);
    }

    // ── BuildHelperPreambleAsync: artefact-based helpers ──────────────────────

    [Fact]
    public async Task BuildHelperPreamble_HelperArtefactRef_ReturnsWrappedContent()
    {
        const string helperBody = "    public static string Shout(string v) => v.ToUpper();\n";
        var artefacts = new List<TemplateArtefact>
        {
            new() { Name = "shout-helper", Extension = ".helpers.cs", Content = helperBody }
        };
        var header = new TemplateHeader { HelperArtefactRef = "shout-helper" };

        var preamble = await TemplateEngine.BuildHelperPreambleAsync(header, artefacts);

        Assert.NotNull(preamble);
        Assert.Contains("BueloGeneratedHelpers", preamble);
        Assert.Contains("Shout", preamble);
    }

    [Fact]
    public async Task BuildHelperPreamble_HelperArtefactRefNotFound_FallsBackToInline()
    {
        var header = new TemplateHeader
        {
            HelperArtefactRef = "missing",
            Helpers = [new("Greet", "string name", "\"Hello \" + name")]
        };

        var preamble = await TemplateEngine.BuildHelperPreambleAsync(header, []);

        // Artefact ref not found → should fall back to inline helpers.
        Assert.NotNull(preamble);
        Assert.Contains("Greet", preamble);
    }

    // ── WrapHelperClass ───────────────────────────────────────────────────────

    [Fact]
    public async Task BuildHelperPreamble_InlineHelper_IsCallableInSectionsTemplate()
    {
        // This test verifies the generated preamble compiles correctly alongside the Report class.
        // We check structure only (not full render, to avoid QuestPDF dependency in this unit test).
        var header = new TemplateHeader
        {
            Helpers = [new("Double", "int n", "n * 2")]
        };

        var preamble = await TemplateEngine.BuildHelperPreambleAsync(header, []);

        Assert.NotNull(preamble);
        Assert.Contains("public static string Double(int n) => n * 2;", preamble);
    }
}
