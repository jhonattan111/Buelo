using System.Text.Json;
using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Renderers;
using Microsoft.AspNetCore.Mvc;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Api;

public class ReportControllerTests
{
    private const string BueloTemplate = """
        report title:
          text: "Hello {{ data.name }}"
        """;

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
            Template = BueloTemplate,
            FileName = "hello.pdf",
            Data = CreateJsonData("World"),
            Mode = TemplateMode.BueloDsl
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
    public async Task RenderById_WithoutFormatQuery_UsesTemplateOutputFormat()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "FormatFromTemplate",
            Template = BueloTemplate,
            Mode = TemplateMode.BueloDsl,
            DefaultFileName = "mock.pdf",
            MockData = CreateJsonData("Fallback"),
            OutputFormat = OutputFormat.Excel
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store, CreateRegistry(engine));

        var result = await controller.RenderById(template.Id);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.EndsWith(".xlsx", file.FileDownloadName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Validate_ValidBueloTemplate_ReturnsValidTrue()
    {
        var controller = CreateController();
        var request = new ReportValidateRequest
        {
            Template = BueloTemplate,
            Mode = TemplateMode.BueloDsl
        };

        var result = await controller.Validate(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<ValidationResult>(ok.Value);
        Assert.True(validation.Valid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task Validate_InvalidBueloTemplate_ReturnsValidFalseWithErrors()
    {
        var controller = CreateController();
        var request = new ReportValidateRequest
        {
            Template = "report title:\n  text: \"{{ unclosed\"",
            Mode = TemplateMode.BueloDsl
        };

        var result = await controller.Validate(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<ValidationResult>(ok.Value);
        Assert.False(validation.Valid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public async Task PostRender_FormatExcel_WithBueloDsl_ReturnsXlsxContentType()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = BueloTemplate,
            FileName = "report",
            Data = CreateJsonData("Test"),
            Mode = TemplateMode.BueloDsl
        };

        var result = await controller.Render(request, format: "excel");

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task RenderFile_WithTemplatePathAndDataSourcePath_RendersPdf()
    {
        var root = Path.Combine(Path.GetTempPath(), $"buelo-workspace-{Guid.NewGuid()}");
        try
        {
            var workspaceStore = new FileSystemWorkspaceStore(root);
            await workspaceStore.CreateFolderAsync("reports");
            await workspaceStore.CreateFolderAsync("data");
            await workspaceStore.CreateFileAsync("reports/main.buelo", BueloTemplate);
            await workspaceStore.CreateFileAsync("data/mock.json", "{\"name\":\"Workspace\"}");

            var store = new InMemoryTemplateStore();
            var engine = new TemplateEngine(new DefaultHelperRegistry(), store, workspaceStore);
            var controller = new ReportController(engine, store, CreateRegistry(engine));

            var result = await controller.RenderFile(new ReportRequest
            {
                TemplatePath = "reports/main.buelo",
                DataSourcePath = "data/mock.json",
                FileName = "workspace-report.pdf"
            });

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal("workspace-report.pdf", file.FileDownloadName);
            Assert.NotEmpty(file.FileContents);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static OutputRendererRegistry CreateRegistry(TemplateEngine engine)
        => new([new PdfRenderer(engine), new ExcelRenderer()]);

    private static ReportController CreateController()
    {
        var store = new InMemoryTemplateStore();
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        return new ReportController(engine, store, CreateRegistry(engine));
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
