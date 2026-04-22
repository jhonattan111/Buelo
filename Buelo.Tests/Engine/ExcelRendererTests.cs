using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Renderers;
using System.Text.Json;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class ExcelRendererTests
{
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

    private static JsonElement JsonObj(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task RenderAsync_ArrayData_ReturnsValidXlsx()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = "",
            Mode = TemplateMode.FullClass,
            RawData = JsonArray(new { name = "Alice", salary = 5000.00 }, new { name = "Bob", salary = 6500.00 }),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x50, bytes[0]); // PK - zip header
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task RenderAsync_ObjectWithArrayProperty_CreatesWorksheet()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = "",
            Mode = TemplateMode.FullClass,
            RawData = JsonObj(new { employees = new[] { new { name = "Alice", department = "HR" } } }),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_FlatObject_CreatesKeyValueSheet()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = "",
            Mode = TemplateMode.FullClass,
            RawData = JsonObj(new { company = "Acme", year = 2024 }),
            PageSettings = PageSettings.Default()
        };

        var bytes = await renderer.RenderAsync(input);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_UnsupportedMode_ThrowsNotSupported()
    {
        var renderer = CreateRenderer();
        var input = new RendererInput
        {
            Source = "",
            Mode = (TemplateMode)999,
            RawData = null,
            PageSettings = PageSettings.Default()
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => renderer.RenderAsync(input));
    }
}
