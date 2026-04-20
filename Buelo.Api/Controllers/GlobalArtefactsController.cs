using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

/// <summary>
/// CRUD endpoints for managing global (template-independent) artefacts.
/// Global artefacts are shared files (data, helpers, partials) that can be referenced
/// by any template via @data, @helper from, or @import directives.
/// </summary>
[ApiController]
[Route("api/artefacts")]
public class GlobalArtefactsController(IGlobalArtefactStore store) : ControllerBase
{
    /// <summary>Returns all global artefacts, optionally filtered by extension.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? extension = null)
    {
        var artefacts = await store.ListAsync(extension);
        return Ok(artefacts);
    }

    /// <summary>Returns a single global artefact by its GUID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var artefact = await store.GetAsync(id);
        if (artefact is null)
            return NotFound(new { error = $"Artefact '{id}' not found." });

        return Ok(artefact);
    }

    /// <summary>Returns a global artefact by name and extension.</summary>
    [HttpGet("by-name/{name}")]
    public async Task<IActionResult> GetByName(string name, [FromQuery] string? extension = null)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return BadRequest(new { error = "Query parameter 'extension' is required (e.g. ?extension=.json)." });

        var artefact = await store.GetByNameAsync(name, extension);
        if (artefact is null)
            return NotFound(new { error = $"Artefact '{name}{extension}' not found." });

        return Ok(artefact);
    }

    /// <summary>Creates a new global artefact. A GUID is automatically assigned.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GlobalArtefact artefact)
    {
        artefact.Id = Guid.Empty; // force a new GUID to be generated
        var saved = await store.SaveAsync(artefact);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
    }

    /// <summary>Updates an existing global artefact identified by <paramref name="id"/>.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] GlobalArtefact artefact)
    {
        var existing = await store.GetAsync(id);
        if (existing is null)
            return NotFound(new { error = $"Artefact '{id}' not found." });

        artefact.Id = id;
        artefact.CreatedAt = existing.CreatedAt;
        var saved = await store.SaveAsync(artefact);
        return Ok(saved);
    }

    /// <summary>Deletes a global artefact by its GUID.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await store.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { error = $"Artefact '{id}' not found." });

        return NoContent();
    }
}
