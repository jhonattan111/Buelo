using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class ProjectControllerTests
{
    private static ProjectController CreateController() =>
        new(new InMemoryBueloProjectStore());

    [Fact]
    public async Task GetProject_ReturnsCurrentSettings()
    {
        var controller = CreateController();

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var project = Assert.IsType<BueloProject>(ok.Value);
        Assert.Equal("Buelo Project", project.Name);
    }

    [Fact]
    public async Task PutProject_UpdatesAllFields()
    {
        var controller = CreateController();

        var input = new BueloProject
        {
            Name = "Updated Project",
            Description = "New description",
            Version = "2.0.0",
            DefaultOutputFormat = "excel"
        };

        var result = await controller.Put(input);

        var ok = Assert.IsType<OkObjectResult>(result);
        var saved = Assert.IsType<BueloProject>(ok.Value);
        Assert.Equal("Updated Project", saved.Name);
        Assert.Equal("New description", saved.Description);
        Assert.Equal("2.0.0", saved.Version);
        Assert.Equal("excel", saved.DefaultOutputFormat);
    }

    [Fact]
    public async Task PatchPageSettings_UpdatesOnlyPageSettings()
    {
        var store = new InMemoryBueloProjectStore();
        // Set initial project with a custom name.
        await store.SaveAsync(new BueloProject { Name = "My Project", PageSettings = new PageSettings { PageSize = "A4" } });

        var controller = new ProjectController(store);

        var newSettings = new PageSettings { PageSize = "Letter", MarginHorizontal = 1.0f };
        var result = await controller.PatchPageSettings(newSettings);

        var ok = Assert.IsType<OkObjectResult>(result);
        var saved = Assert.IsType<BueloProject>(ok.Value);
        // Name preserved, only page settings changed.
        Assert.Equal("My Project", saved.Name);
        Assert.Equal("Letter", saved.PageSettings.PageSize);
        Assert.Equal(1.0f, saved.PageSettings.MarginHorizontal);
    }

    [Fact]
    public async Task Reset_ReturnsFactoryDefaults()
    {
        var store = new InMemoryBueloProjectStore();
        await store.SaveAsync(new BueloProject { Name = "Customized", Version = "5.0.0" });

        var controller = new ProjectController(store);
        var result = await controller.Reset();

        var ok = Assert.IsType<OkObjectResult>(result);
        var project = Assert.IsType<BueloProject>(ok.Value);
        Assert.Equal("Buelo Project", project.Name);
        Assert.Equal("1.0.0", project.Version);
    }
}
