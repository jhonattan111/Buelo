using Buelo.Engine.BueloDsl;

namespace Buelo.Tests.Engine;

public class BueloDslParserTests
{
    [Fact]
    public void Parse_MinimalReport_ReturnsDocument()
    {
        var source = """
            report title:
              text: "Hello World"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Single(doc.Components);
        var layout = Assert.IsType<BueloDslLayoutComponent>(doc.Components[0]);
        Assert.Equal("report title", layout.ComponentType);
        Assert.Single(layout.Children);
        var text = Assert.IsType<BueloDslTextComponent>(layout.Children[0]);
        Assert.Equal("Hello World", text.Value);
    }

    [Fact]
    public void Parse_ImportStatement_ExtractsFunctionNames()
    {
        var source = """
            import { FormatCNPJ, FormatCurrency } from "formatters"
            report title:
              text: "Test"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Single(doc.Directives.Imports);
        var imp = doc.Directives.Imports[0];
        Assert.Equal("formatters", imp.Source);
        Assert.Equal(2, imp.Functions.Count);
        Assert.Contains("FormatCNPJ", imp.Functions);
        Assert.Contains("FormatCurrency", imp.Functions);
    }

    [Fact]
    public void Parse_WildcardImport_SetsEmptyFunctionList()
    {
        var source = """
            import * from "formatters"
            report title:
              text: "Test"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Single(doc.Directives.Imports);
        Assert.Empty(doc.Directives.Imports[0].Functions); // wildcard = empty list
    }

    [Fact]
    public void Parse_Settings_ExtractsSizeAndOrientation()
    {
        var source = """
            @settings
              size: A4
              orientation: Portrait
              margin: 2cm
            report title:
              text: "Test"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.NotNull(doc.Directives.Settings);
        Assert.Equal("A4", doc.Directives.Settings!.Size);
        Assert.Equal("Portrait", doc.Directives.Settings.Orientation);
        Assert.Equal("2cm", doc.Directives.Settings.Margin);
    }

    [Fact]
    public void Parse_DataRef_ExtractsRef()
    {
        var source = """
            @data from "colaborador.json"
            report title:
              text: "Test"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Equal("colaborador.json", doc.Directives.DataRef);
    }

    [Fact]
    public void Parse_Table_ExtractsColumnsAndGroupHeader()
    {
        var source = """
            data:
              table:
                columns:
                  - field: nome
                    label: Nome Completo
                    width: 40%
                  - field: cargo
                    label: Cargo
                    width: 30%
                group header:
                  text: "{{ value }}"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Single(doc.Components);
        var dataComp = Assert.IsType<BueloDslLayoutComponent>(doc.Components[0]);
        Assert.Equal("data", dataComp.ComponentType);

        Assert.Single(dataComp.Children);
        var tableComp = Assert.IsType<BueloDslTableComponent>(dataComp.Children[0]);

        Assert.Equal(2, tableComp.Columns.Count);
        Assert.Equal("nome", tableComp.Columns[0].Field);
        Assert.Equal("Nome Completo", tableComp.Columns[0].Label);
        Assert.Equal("40%", tableComp.Columns[0].Width);
        Assert.Equal("cargo", tableComp.Columns[1].Field);

        Assert.NotNull(tableComp.GroupHeader);
        var ghLayout = Assert.IsType<BueloDslLayoutComponent>(tableComp.GroupHeader!);
        Assert.Single(ghLayout.Children);
        var ghText = Assert.IsType<BueloDslTextComponent>(ghLayout.Children[0]);
        Assert.Equal("{{ value }}", ghText.Value);
    }

    [Fact]
    public void Parse_UnrecognizedKeyword_ReturnsWarning()
    {
        var source = """
            unknownKeyword:
              prop: value
            """;

        BueloDslParser.Parse(source, out var errors);

        Assert.Contains(errors, e => e.Severity == BueloDslErrorSeverity.Warning);
        Assert.Contains(errors, e => e.Message.Contains("unknownKeyword"));
    }

    [Fact]
    public void Parse_UnbalancedExpression_ReturnsError()
    {
        var source = """
            report title:
              text: "{{ unclosed"
            """;

        BueloDslParser.Parse(source, out var errors);

        Assert.Contains(errors, e => e.Severity == BueloDslErrorSeverity.Error);
    }

    [Fact]
    public void Parse_PageHeaderAndFooter_AreRecognized()
    {
        var source = """
            page header:
              text: "Header"
            page footer:
              text: "Footer"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Equal(2, doc.Components.Count);
        Assert.Equal("page header", doc.Components[0].ComponentType);
        Assert.Equal("page footer", doc.Components[1].ComponentType);
    }

    [Fact]
    public void IsBueloDslSource_ReturnsTrueForReportTitle()
    {
        Assert.True(BueloDslParser.IsBueloDslSource("report title:\n  text: \"Hi\""));
    }

    [Fact]
    public void IsBueloDslSource_ReturnsFalseForSectionsSource()
    {
        Assert.False(BueloDslParser.IsBueloDslSource("page.Content().Text(\"Hi\");"));
    }

    [Fact]
    public void Parse_CommentLinesAreIgnored()
    {
        var source = """
            # This is a comment
            report title:
              # inline comment
              text: "Hello"
            """;

        var doc = BueloDslParser.Parse(source);

        Assert.Single(doc.Components);
        var layout = Assert.IsType<BueloDslLayoutComponent>(doc.Components[0]);
        Assert.Single(layout.Children);
    }

    [Fact]
    public void Parse_StyleBlock_ExtractsFontSizeAndBold()
    {
        var source = """
            report title:
              text: "Hello"
              style:
                fontSize: 18
                bold: true
                color: "#333333"
            """;

        var doc = BueloDslParser.Parse(source);

        var layout = Assert.IsType<BueloDslLayoutComponent>(doc.Components[0]);
        Assert.NotNull(layout.Style);
        Assert.Equal(18, layout.Style!.FontSize);
        Assert.True(layout.Style.Bold);
        Assert.Equal("#333333", layout.Style.Color);
    }
}
