using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;

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

    // ── Artefacts ────────────────────────────────────────────────────────────

    /// <summary>Returns all artefacts for a template (name + extension only, no content).</summary>
    [HttpGet("{id:guid}/artefacts")]
    public async Task<IActionResult> ListArtefacts(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var result = template.Artefacts.Select(a => new { a.Name, a.Extension }).ToList();
        return Ok(result);
    }

    /// <summary>Returns a single artefact (with content) by its slug name.</summary>
    [HttpGet("{id:guid}/artefacts/{name}")]
    public async Task<IActionResult> GetArtefact(Guid id, string name)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var artefact = template.Artefacts.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (artefact is null)
            return NotFound(new { error = $"Artefact '{name}' not found." });

        return Ok(artefact);
    }

    /// <summary>Creates or replaces an artefact. Body: <c>{ "extension": ".json", "content": "..." }</c>.</summary>
    [HttpPut("{id:guid}/artefacts/{name}")]
    public async Task<IActionResult> UpsertArtefact(Guid id, string name, [FromBody] UpsertArtefactRequest body)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var existing = template.Artefacts.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Extension = body.Extension;
            existing.Content = body.Content;
        }
        else
        {
            template.Artefacts.Add(new TemplateArtefact
            {
                Name = name,
                Extension = body.Extension,
                Content = body.Content
            });
        }

        await store.SaveAsync(template);
        var saved = template.Artefacts.First(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        return Ok(saved);
    }

    /// <summary>Deletes an artefact by its slug name.</summary>
    [HttpDelete("{id:guid}/artefacts/{name}")]
    public async Task<IActionResult> DeleteArtefact(Guid id, string name)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var artefact = template.Artefacts.FirstOrDefault(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (artefact is null)
            return NotFound(new { error = $"Artefact '{name}' not found." });

        template.Artefacts.Remove(artefact);
        await store.SaveAsync(template);
        return NoContent();
    }

    // ── Versions ─────────────────────────────────────────────────────────────

    /// <summary>Returns all version metadata (version number + timestamp) for a template.</summary>
    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var versions = await store.GetVersionsAsync(id);
        var result = versions.Select(v => new { v.Version, v.SavedAt, v.SavedBy }).ToList();
        return Ok(result);
    }

    /// <summary>Returns the full snapshot (template source + artefacts) for a specific version.</summary>
    [HttpGet("{id:guid}/versions/{n:int}")]
    public async Task<IActionResult> GetVersion(Guid id, int n)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var version = await store.GetVersionAsync(id, n);
        if (version is null)
            return NotFound(new { error = $"Version {n} not found for template '{id}'." });

        return Ok(version);
    }

    /// <summary>
    /// Restores the template to a historical snapshot.
    /// The current state is saved as a new version before overwriting.
    /// </summary>
    [HttpPost("{id:guid}/versions/{n:int}/restore")]
    public async Task<IActionResult> RestoreVersion(Guid id, int n)
    {
        var existing = await store.GetAsync(id);
        if (existing is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var version = await store.GetVersionAsync(id, n);
        if (version is null)
            return NotFound(new { error = $"Version {n} not found for template '{id}'." });

        existing.Template = version.Template;
        existing.Artefacts = version.Artefacts;
        var saved = await store.SaveAsync(existing);
        return Ok(saved);
    }

    // ── Export / Import ───────────────────────────────────────────────────────

    /// <summary>
    /// Exports the template and all its artefacts as a ZIP bundle.
    /// The ZIP layout mirrors the <c>FileSystemTemplateStore</c> folder structure.
    /// </summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Metadata: everything except Template source and Artefacts list.
            var meta = new TemplateMeta(
                template.Id, template.Name, template.Description, template.Mode,
                template.DataSchema, template.MockData, template.DefaultFileName,
                template.PageSettings, template.CreatedAt, template.UpdatedAt);

            var opts = new JsonSerializerOptions { WriteIndented = true };
            var metaJson = JsonSerializer.Serialize(meta, opts);
            await WriteZipEntryAsync(zip, "template.record.json", metaJson);

            // Template source.
            if (!string.IsNullOrEmpty(template.Template))
                await WriteZipEntryAsync(zip, "template.report.cs", template.Template);

            // Artefacts.
            foreach (var a in template.Artefacts)
                await WriteZipEntryAsync(zip, $"{a.Name}{a.Extension}", a.Content);
        }

        ms.Position = 0;
        var safeName = string.Concat(template.Name.Split(Path.GetInvalidFileNameChars()));
        return File(ms.ToArray(), "application/zip", $"{safeName}-{id}.zip");
    }

    /// <summary>
    /// Imports a template from a ZIP bundle (as produced by <c>GET /export</c>).
    /// A new GUID is always assigned; the original ID is discarded.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        var metaEntry = zip.GetEntry("template.record.json");
        if (metaEntry is null)
            return BadRequest(new { error = "Invalid bundle: missing template.record.json." });

        TemplateMeta meta;
        using (var reader = new StreamReader(metaEntry.Open()))
        {
            var json = await reader.ReadToEndAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            meta = JsonSerializer.Deserialize<TemplateMeta>(json, opts)
                   ?? throw new InvalidOperationException("Failed to deserialize template metadata.");
        }

        var template = new TemplateRecord
        {
            Id = Guid.Empty,
            Name = meta.Name,
            Description = meta.Description,
            Mode = meta.Mode,
            DataSchema = meta.DataSchema,
            MockData = meta.MockData,
            DefaultFileName = meta.DefaultFileName,
            PageSettings = meta.PageSettings ?? PageSettings.Default()
        };

        var srcEntry = zip.GetEntry("template.report.cs");
        if (srcEntry is not null)
        {
            using var reader = new StreamReader(srcEntry.Open());
            template.Template = await reader.ReadToEndAsync();
        }

        foreach (var entry in zip.Entries.Where(e =>
            e.Name != "template.record.json" && e.Name != "template.report.cs"))
        {
            var dotIndex = entry.Name.IndexOf('.');
            var artefactName = dotIndex >= 0 ? entry.Name[..dotIndex] : entry.Name;
            var ext = dotIndex >= 0 ? entry.Name[dotIndex..] : string.Empty;

            using var reader = new StreamReader(entry.Open());
            var content = await reader.ReadToEndAsync();
            template.Artefacts.Add(new TemplateArtefact
            {
                Name = artefactName,
                Extension = ext,
                Content = content
            });
        }

        var saved = await store.SaveAsync(template);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task WriteZipEntryAsync(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync(content);
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private record TemplateMeta(
        Guid Id, string Name, string? Description, TemplateMode Mode,
        string? DataSchema, object? MockData, string DefaultFileName,
        PageSettings? PageSettings, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}

/// <summary>Request body for upserting a template artefact.</summary>
public record UpsertArtefactRequest(string Extension, string Content);

