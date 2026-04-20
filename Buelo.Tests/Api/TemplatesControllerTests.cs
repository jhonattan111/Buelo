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
            Mode = TemplateMode.BueloDsl,
            Template = "report title:\n  text: Invoice"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var saved = Assert.IsType<TemplateRecord>(created.Value);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(TemplateMode.BueloDsl, saved.Mode);
        Assert.Equal(nameof(TemplatesController.Get), created.ActionName);
    }

    [Fact]
    public async Task ListFiles_ShouldReturnCoreFilesAndArtefacts()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(new TemplateRecord
        {
            Name = "Files",
            Template = "report title:\n  text: hello",
            Mode = TemplateMode.BueloDsl,
            MockData = new { name = "Alice" },
            Artefacts =
            [
                new TemplateArtefact
                {
                    Path = "helpers/tax.helpers.cs",
                    Name = "tax",
                    Extension = ".helpers.cs",
                    Content = "// helper"
                }
            ]
        });

        var controller = new TemplatesController(store);
        var result = await controller.ListFiles(saved.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var files = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value).ToList();
        Assert.True(files.Count >= 3);
    }

    [Fact]
    public async Task UpsertFile_TemplateCore_ShouldUpdateTemplateAndMode()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(new TemplateRecord
        {
            Name = "Mode",
            Template = "old",
            Mode = TemplateMode.BueloDsl
        });

        var controller = new TemplatesController(store);
        var result = await controller.UpsertFile(
            saved.Id,
            new UpsertTemplateFileRequest("template.report.cs", "report title:\n  text: Updated", "template", "BueloDsl"));

        Assert.IsType<OkObjectResult>(result);

        var reloaded = await store.GetAsync(saved.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("report title:\n  text: Updated", reloaded.Template);
        Assert.Equal(TemplateMode.BueloDsl, reloaded.Mode);
    }
}
