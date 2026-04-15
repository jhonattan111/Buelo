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
    public async Task Render_WithCustomPageSettings_ShouldReturnPdfFile()
    {
        var controller = CreateController();
        var request = new ReportRequest
        {
            Template = BuilderTemplate,
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
    public async Task RenderById_WithCustomPageSettings_ShouldUseOverride()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Name = "Mock",
            Template = BuilderTemplate,
            Mode = TemplateMode.Builder,
            DefaultFileName = "mock.pdf",
            MockData = CreateJsonData("Fallback"),
            PageSettings = PageSettings.A4Compact()
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store);

        var customSettings = new PageSettings
        {
            PageSize = "Letter",
            WatermarkText = "OVERRIDE"
        };

        var result = await controller.RenderById(template.Id, new TemplateRenderRequest { PageSettings = customSettings });

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
            Template = BuilderTemplate,
            Mode = TemplateMode.Builder,
            MockData = null
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store);

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
            Template = BuilderTemplate,
            Mode = TemplateMode.Builder,
            MockData = CreateJsonData("Test"),
            PageSettings = watermarkSettings
        });

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var controller = new ReportController(engine, store);

        var result = await controller.Preview(template.Id);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.NotEmpty(file.FileContents);
    }

    private static ReportController CreateController()
    {
        var store = new InMemoryTemplateStore();
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        return new ReportController(engine, store);
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
}
