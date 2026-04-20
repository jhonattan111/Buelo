using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/project")]
public class ProjectController(IBueloProjectStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var project = await store.GetAsync();
        return Ok(project);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] BueloProject project)
    {
        var saved = await store.SaveAsync(project);
        return Ok(saved);
    }

    [HttpPatch("page-settings")]
    public async Task<IActionResult> PatchPageSettings([FromBody] PageSettings pageSettings)
    {
        var project = await store.GetAsync();
        project.PageSettings = pageSettings;
        var saved = await store.SaveAsync(project);
        return Ok(saved);
    }

    [HttpPatch("mock-data")]
    public async Task<IActionResult> PatchMockData([FromBody] JsonElement mockData)
    {
        var project = await store.GetAsync();
        project.MockData = mockData;
        var saved = await store.SaveAsync(project);
        return Ok(saved);
    }

    [HttpGet("reset")]
    public async Task<IActionResult> Reset()
    {
        var defaults = new BueloProject();
        var saved = await store.SaveAsync(defaults);
        return Ok(saved);
    }
}
