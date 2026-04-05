using System.Text.Json;
using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Api;

public class ReportControllerTests
{
    private const string BuilderTemplate = "Document.Create(c => c.Page(p => p.Content().Text((string)data.name))).GeneratePdf()";

    public ReportControllerTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task Render_ShouldReturnPdfFile()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = BuilderTemplate,
            FileName = "hello.pdf",
            Data = CreateJsonData("World")
        };

        var result = await controller.Render(request);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("hello.pdf", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task RenderById_WhenTemplateNotFound_ShouldReturnNotFound()
    {
        var controller = CreateController();

        var result = await controller.RenderById(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RenderById_WhenNoRequestData_ShouldFallbackToMockData()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "Mock",
            Template = BuilderTemplate,
            Mode = TemplateMode.Builder,
            DefaultFileName = "mock.pdf",
            MockData = CreateJsonData("Fallback")
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store);

        var result = await controller.RenderById(template.Id, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("mock.pdf", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task Preview_WhenMockDataMissing_ShouldReturnBadRequest()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "NoMock",
            Template = BuilderTemplate,
            Mode = TemplateMode.Builder,
            MockData = null
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store);

        var result = await controller.Preview(template.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static ReportController CreateController()
    {
        var store = new InMemoryTemplateStore();
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        return new ReportController(engine, store);
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
