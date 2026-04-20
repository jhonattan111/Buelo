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
    private const string SectionsTemplate = "page.Content().Text((string)data.name);";

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
            Template = SectionsTemplate,
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
    public async Task Render_WithCustomPageSettings_ShouldReturnPdfFile()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = SectionsTemplate,
            FileName = "hello.pdf",
            Data = CreateJsonData("World"),
            PageSettings = new PageSettings
            {
                PageSize = "Letter",
                MarginHorizontal = 1.0f,
                MarginVertical = 1.0f,
                BackgroundColor = "#F5F5F5",
                WatermarkText = "DRAFT"
            }
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
            Template = SectionsTemplate,
            DefaultFileName = "mock.pdf",
            MockData = CreateJsonData("Fallback")
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store, CreateRegistry(engine));

        var result = await controller.RenderById(template.Id, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("mock.pdf", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task RenderById_WithCustomPageSettings_ShouldUseOverride()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "Mock",
            Template = SectionsTemplate,
            DefaultFileName = "mock.pdf",
            MockData = CreateJsonData("Fallback"),
            PageSettings = PageSettings.A4Compact()
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store, CreateRegistry(engine));

        var customSettings = new PageSettings
        {
            PageSize = "Letter",
            WatermarkText = "OVERRIDE"
        };

        var result = await controller.RenderById(template.Id, null, new TemplateRenderRequest { PageSettings = customSettings });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task Preview_WhenMockDataMissing_ShouldReturnBadRequest()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "NoMock",
            Template = SectionsTemplate,
            MockData = null
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store, CreateRegistry(engine));

        var result = await controller.Preview(template.Id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Preview_ShouldUseTemplatePageSettings()
    {
        var store = new InMemoryTemplateStore();
        var watermarkSettings = PageSettings.WithWatermark("PREVIEW");

        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "WithWatermark",
            Template = SectionsTemplate,
            MockData = CreateJsonData("Test"),
            PageSettings = watermarkSettings
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store, CreateRegistry(engine));

        var result = await controller.Preview(template.Id);

        var file = Assert.IsType<FileContentResult>(result);
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

    // ── Validate endpoint ─────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidSectionsTemplate_ReturnsValidTrue()
    {
        var controller = CreateController();
        var request = new ReportValidateRequest
        {
            Template = "page.Content().Text(\"hello\");",
            Mode = TemplateMode.Sections
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
            // undeclared_variable triggers CS0103 — valid slot syntax, invalid C# inside.
            Template = "page.Content().Text(undeclared_variable);",
            Mode = TemplateMode.Sections
        };

        var result = await controller.Validate(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<ValidationResult>(ok.Value);
        Assert.False(validation.Valid);
        Assert.NotEmpty(validation.Errors);
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    // ── Sprint 17: format param + GetFormats ──────────────────────────────────

    [Fact]
    public async Task PostRender_FormatPdf_ReturnsApplicationPdf()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = SectionsTemplate,
            FileName = "report.pdf",
            Data = CreateJsonData("Test")
        };

        var result = await controller.Render(request, format: "pdf");

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task PostRender_FormatExcel_WithBueloDsl_ReturnsXlsxContentType()
    {
        var controller = CreateController();
        var bueloDslSource = """
            report title:
              text: My Report
            """;

        var request = new ReportRequest
        {
            Template = bueloDslSource,
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
    public async Task PostRender_FormatExcel_WithSectionsMode_ReturnsBadRequest()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = SectionsTemplate,
            FileName = "report.pdf",
            Data = CreateJsonData("Test"),
            Mode = TemplateMode.Sections
        };

        var result = await controller.Render(request, format: "excel");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PostRender_UnknownFormat_ReturnsBadRequest()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = SectionsTemplate,
            FileName = "report.pdf",
            Data = CreateJsonData("Test")
        };

        var result = await controller.Render(request, format: "word");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetFormats_ReturnsAllRegisteredFormats()
    {
        var controller = CreateController();

        var result = controller.GetFormats();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("pdf", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("excel", json, StringComparison.OrdinalIgnoreCase);
    }
}
