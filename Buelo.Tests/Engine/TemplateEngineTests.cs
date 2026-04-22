using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class TemplateEngineTests
{
    private const string ValidCsharpTemplate = """
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

    private const string InvalidCsharpTemplate = """
        public class Broken : IDocument
        {
            // missing Compose method, invalid C#
            public void foo(
        }
        """;

    public TemplateEngineTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task RenderAsync_ValidCsharpTemplate_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var pdf = await engine.RenderAsync(ValidCsharpTemplate, CreateJsonData("World"), TemplateMode.FullClass);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task RenderTemplateAsync_Record_ShouldGeneratePdfBytes()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var template = new TemplateRecord
        {
            Name = "HelloReport",
            Template = ValidCsharpTemplate,
            Mode = TemplateMode.FullClass,
            MockData = CreateJsonData("World")
        };

        var pdf = await engine.RenderTemplateAsync(template, null);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task ValidateAsync_InvalidCsharp_ReturnsErrors()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var result = await engine.ValidateAsync(InvalidCsharpTemplate, TemplateMode.FullClass);

        Assert.False(result.Valid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_ValidCsharp_ReturnsNoErrors()
    {
        var engine = new TemplateEngine(new DefaultHelperRegistry());

        var result = await engine.ValidateAsync(ValidCsharpTemplate, TemplateMode.FullClass);

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ConvertToDynamic_JsonElementObject_ShouldKeepStructure()
    {
        var json = """
                   {
                     "name": "World",
                     "count": 2,
                     "active": true,
                     "nested": {
                       "city": "Sao Paulo"
                     }
                   }
                   """;

        var element = JsonSerializer.Deserialize<JsonElement>(json);

        dynamic result = TemplateEngine.ConvertToDynamic(element);

        Assert.Equal("World", (string)result.name);
        Assert.Equal(2L, (long)result.count);
        Assert.True((bool)result.active);
        Assert.Equal("Sao Paulo", (string)result.nested.city);
    }

    private static JsonElement CreateJsonData(string name)
    {
        var json = JsonSerializer.Serialize(new { name });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task RenderAsync_TypedModelConstructor_ShouldDeserializeAndRender()
    {
        // Template with a typed record constructor — previously threw ArgumentException
        // "Object of type 'ExpandoObject' cannot be converted to type 'InvoiceModel'"
        const string typedTemplate = """
            using QuestPDF.Fluent;
            using QuestPDF.Helpers;
            using QuestPDF.Infrastructure;

            public record InvoiceModel(string Client, decimal Total);

            public class InvoiceDocument : IDocument
            {
                private readonly InvoiceModel _model;
                public InvoiceDocument(InvoiceModel model) => _model = model;

                public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

                public void Compose(IDocumentContainer container)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.Content().Text($"Invoice for {_model.Client}: {_model.Total:C}");
                    });
                }
            }
            """;

        var engine = new TemplateEngine(new DefaultHelperRegistry());
        var data = JsonSerializer.Deserialize<JsonElement>("""{ "client": "Acme", "total": 199.99 }""");

        var pdf = await engine.RenderAsync(typedTemplate, data, TemplateMode.FullClass);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }
}
