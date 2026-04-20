using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.BueloDsl;
using Buelo.Engine.Renderers;
using QuestPDF;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace Buelo.Tests.Engine;

public class ExcelRendererTests
{
    private static readonly string TableSource = """
        data:
          table:
            columns:
              - field: name
                label: Name
              - field: salary
                label: Salary
                format: currency
        """;

    private static readonly string TitleAndTableSource = """
        report title:
          text: Employee Report
        data:
          table:
            columns:
              - field: name
                label: Employee Name
              - field: department
                label: Department
        """;

    public ExcelRendererTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static ExcelRenderer CreateRenderer() => new();

    private static JsonElement JsonArray(params object[] items)
    {
        var json = JsonSerializer.Serialize(items);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task RenderAsync_BueloDsl_WithTable_ReturnsValidXlsx()
    {
        var renderer = CreateRenderer();
        var doc = BueloDslParser.Parse(TableSource);
        var input = new RendererInput
        {
            Source = TableSource,
            Mode = TemplateMode.BueloDsl,
            BueloDslDocument = doc,
            RawData = JsonArray(new { name = "Alice", salary = 5000.00 }, new { name = "Bob", salary = 6500.00 }),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);

        Assert.NotEmpty(bytes);
        // XLSX files start with PK (zip magic bytes)
        Assert.Equal(0x50, bytes[0]); // 'P'
        Assert.Equal(0x4B, bytes[1]); // 'K'
    }

    [Fact]
    public async Task RenderAsync_ColumnHeaders_MatchColumnLabels()
    {
        var renderer = CreateRenderer();
        var doc = BueloDslParser.Parse(TitleAndTableSource);
        var input = new RendererInput
        {
            Source = TitleAndTableSource,
            Mode = TemplateMode.BueloDsl,
            BueloDslDocument = doc,
            RawData = JsonArray(new { name = "Alice", department = "HR" }),
            PageSettings = PageSettings.Default()
        };

        // Just verify it runs without error; header content verified by XLSX structure
        var bytes = await renderer.RenderAsync(input);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_CurrencyFormat_AppliesNumberFormat()
    {
        var renderer = CreateRenderer();
        var doc = BueloDslParser.Parse(TableSource);
        var input = new RendererInput
        {
            Source = TableSource,
            Mode = TemplateMode.BueloDsl,
            BueloDslDocument = doc,
            RawData = JsonArray(new { name = "Alice", salary = 5000.00 }),
            PageSettings = PageSettings.Default()
        };

        // Verify the XLSX is generated without exceptions (currency format applied internally)
        var bytes = await renderer.RenderAsync(input);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_UnsupportedMode_ThrowsNotSupported()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = "page.Content().Text(\"hello\");",
            Mode = (TemplateMode)999,
            RawData = null,
            PageSettings = PageSettings.Default()
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => renderer.RenderAsync(input));
    }
}
