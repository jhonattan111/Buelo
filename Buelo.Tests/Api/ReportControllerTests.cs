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
    private const string ValidTemplate = """
        using QuestPDF.Fluent;
        using QuestPDF.Helpers;
        using QuestPDF.Infrastructure;

        public class HelloDocument : IDocument
        {
            private readonly dynamic _data;
            public HelloDocument(dynamic data) => _data = data;
            public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
            public void Compose(IDocumentContainer container)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Content().Text($"Hello {_data.name}");
                });
            }
        }
        """;

    private const string InvalidTemplate = "public class Bad { public void foo( }";

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
            Template = ValidTemplate,
            FileName = "hello.pdf",
            Data = CreateJsonData("World"),
            Mode = TemplateMode.FullClass
        };

        var result = await controller.Render(request);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("hello.pdf", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task Render_InvalidTemplate_ShouldReturnBadRequest()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = InvalidTemplate,
            FileName = "report.pdf",
            Data = CreateJsonData("Test"),
            Mode = TemplateMode.FullClass
        };

        var result = await controller.Render(request);

        Assert.IsType<BadRequestObjectResult>(result);
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
            Template = ValidTemplate,
            Mode = TemplateMode.FullClass,
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
    public async Task Validate_ValidTemplate_ReturnsValidTrue()
    {
        var controller = CreateController();
        var request = new ReportValidateRequest
        {
            Template = ValidTemplate,
            Mode = TemplateMode.FullClass
        };

        var result = await controller.Validate(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<ValidationResult>(ok.Value);
        Assert.True(validation.Valid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task Validate_InvalidTemplate_ReturnsValidFalseWithErrors()
    {
        var controller = CreateController();
        var request = new ReportValidateRequest
        {
            Template = InvalidTemplate,
            Mode = TemplateMode.FullClass
        };

        var result = await controller.Validate(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<ValidationResult>(ok.Value);
        Assert.False(validation.Valid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public async Task Render_FormatExcel_ReturnsXlsxContentType()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = ValidTemplate,
            FileName = "report",
            Data = CreateJsonData("Test"),
            Mode = TemplateMode.FullClass
        };

        var result = await controller.Render(request, format: "excel");

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.NotEmpty(file.FileContents);
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
