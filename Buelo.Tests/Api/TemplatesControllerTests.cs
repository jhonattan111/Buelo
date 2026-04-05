using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class TemplatesControllerTests
{
    [Fact]
    public async Task Create_ShouldAssignIdAndReturnCreatedAtAction()
    {
        var store = new InMemoryTemplateStore();
        var controller = new TemplatesController(store);

        var result = await controller.Create(new TemplateRecord
        {
            Name = "Invoice",
            Template = "Document.Create(c => c.Page(p => p.Content().Text(\"ok\"))).GeneratePdf()"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var saved = Assert.IsType<TemplateRecord>(created.Value);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(nameof(TemplatesController.Get), created.ActionName);
    }

    [Fact]
    public async Task Get_WhenMissing_ShouldReturnNotFound()
    {
        var store = new InMemoryTemplateStore();
        var controller = new TemplatesController(store);

        var result = await controller.Get(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_WhenMissing_ShouldReturnNotFound()
    {
        var store = new InMemoryTemplateStore();
        var controller = new TemplatesController(store);

        var result = await controller.Update(Guid.NewGuid(), new TemplateRecord
        {
            Name = "Any",
            Template = "any"
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_WhenMissing_ShouldReturnNotFound()
    {
        var store = new InMemoryTemplateStore();
        var controller = new TemplatesController(store);

        var result = await controller.Delete(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task List_ShouldReturnStoredTemplates()
    {
        var store = new InMemoryTemplateStore();
        await store.SaveAsync(new TemplateRecord
        {
            Name = "One",
            Template = "Document.Create(c => c.Page(p => p.Content().Text(\"ok\"))).GeneratePdf()"
        });

        var controller = new TemplatesController(store);

        var result = await controller.List();

        var ok = Assert.IsType<OkObjectResult>(result);
        var values = Assert.IsType<IEnumerable<TemplateRecord>>(ok.Value, exactMatch: false);
        Assert.Single(values);
    }
}
