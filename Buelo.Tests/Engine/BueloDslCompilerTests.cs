using Buelo.Engine.BueloDsl;

namespace Buelo.Tests.Engine;

public class BueloDslCompilerTests
{
    private static BueloDslDocument ParseSource(string source) => BueloDslParser.Parse(source);

    [Fact]
    public void Compile_TextComponent_GeneratesQuestPdfText()
    {
        var doc = ParseSource("""
            report title:
              text: "Hello World"
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        Assert.Contains(".Text(", code);
        Assert.Contains("Hello World", code);
    }

    [Fact]
    public void Compile_TableWithColumns_GeneratesTableDefinition()
    {
        var doc = ParseSource("""
            data:
              table:
                columns:
                  - field: nome
                    label: Nome Completo
                    width: 40%
                  - field: cargo
                    label: Cargo
                    width: 30%
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        Assert.Contains("Table(", code);
        Assert.Contains("ColumnsDefinition(", code);
        Assert.Contains("Nome Completo", code);
        Assert.Contains("Cargo", code);
    }

    [Fact]
    public void Compile_PageHeaderAndFooter_GeneratesCorrectSlots()
    {
        var doc = ParseSource("""
            page header:
              text: "Header Text"
            report title:
              text: "Content"
            page footer:
              text: "Footer Text"
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        Assert.Contains("page.Header(", code);
        Assert.Contains("page.Content(", code);
        Assert.Contains("page.Footer(", code);
        // Verify order: header before content, content before footer
        Assert.True(code.IndexOf("page.Header(", StringComparison.Ordinal) <
                    code.IndexOf("page.Content(", StringComparison.Ordinal));
        Assert.True(code.IndexOf("page.Content(", StringComparison.Ordinal) <
                    code.IndexOf("page.Footer(", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_ExpressionInterpolation_GeneratesValidCsharp()
    {
        var doc = ParseSource("""
            report title:
              text: "Hello {{ data.name }}!"
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        // Expression should be compiled to C# expression
        Assert.Contains("data.name", code);
    }

    [Fact]
    public void Compile_NoComponents_ReturnsEmptyString()
    {
        var doc = new BueloDslDocument(
            new BueloDslDirectives([], null, null),
            []);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        Assert.Equal(string.Empty, code.Trim());
    }

    [Fact]
    public void Compile_OnlyHeader_GeneratesOnlyHeaderSlot()
    {
        var doc = ParseSource("""
            page header:
              text: "Only Header"
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        Assert.Contains("page.Header(", code);
        Assert.DoesNotContain("page.Footer(", code);
    }

    [Fact]
    public void Compile_TableWithPercentWidths_GeneratesRelativeColumns()
    {
        var doc = ParseSource("""
            data:
              table:
                columns:
                  - field: a
                    label: A
                    width: 40%
                  - field: b
                    label: B
                    width: 60%
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        Assert.Contains("RelativeColumn(", code);
    }

    [Fact]
    public void Compile_MultipleContentComponents_MergedIntoOneContentSlot()
    {
        var doc = ParseSource("""
            report title:
              text: "Title"
            report resume:
              text: "Summary"
            """);

        var code = BueloDslCompiler.Compile(doc, new CompileOptions());

        // Should appear only once
        Assert.Equal(1, CountOccurrences(code, "page.Content("));
        Assert.Contains("Title", code);
        Assert.Contains("Summary", code);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
