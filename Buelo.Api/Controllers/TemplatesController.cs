using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

/// <summary>
/// CRUD endpoints for managing named, persisted report templates.
/// Each template is identified by a stable GUID that can be used to render
/// or preview the report without resending the full template source.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TemplatesController(ITemplateStore store) : ControllerBase
{
    /// <summary>Returns all saved templates (metadata only – no full source by design for large payloads).</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var templates = await store.ListAsync();
        return Ok(templates);
    }

    /// <summary>Returns a single template by its GUID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        return Ok(template);
    }

    /// <summary>Creates a new template. A GUID is automatically assigned.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TemplateRecord template)
    {
        template.Id = Guid.Empty; // force a new GUID to be generated
        var saved = await store.SaveAsync(template);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
    }

    /// <summary>Replaces an existing template identified by <paramref name="id"/>.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TemplateRecord template)
    {
        var existing = await store.GetAsync(id);
        if (existing is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        template.Id = id;
        template.CreatedAt = existing.CreatedAt;
        var saved = await store.SaveAsync(template);
        return Ok(saved);
    }

    /// <summary>Deletes a template by its GUID.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await store.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { error = $"Template '{id}' not found." });

        return NoContent();
    }
}
