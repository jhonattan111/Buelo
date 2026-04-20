using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;
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

        var result = template.Artefacts
            .Select(a => new { a.Name, a.Extension, Path = ResolveArtefactPath(a) })
            .ToList();
        return Ok(result);
    }

    /// <summary>Returns a single artefact (with content) by its slug name.</summary>
    [HttpGet("{id:guid}/artefacts/{name}")]
    public async Task<IActionResult> GetArtefact(Guid id, string name)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var artefact = template.Artefacts.FirstOrDefault(a => MatchesArtefactRef(a, name));
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

        var existing = template.Artefacts.FirstOrDefault(a => MatchesArtefactRef(a, name));

        var resolvedPath = !string.IsNullOrWhiteSpace(body.Path)
            ? NormalizeRelativePath(body.Path)
            : NormalizeRelativePath($"{name}{body.Extension}");

        var fileName = Path.GetFileName(resolvedPath);
        var dotIndex = fileName.IndexOf('.');
        var parsedName = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
        var parsedExtension = dotIndex >= 0 ? fileName[dotIndex..] : body.Extension;

        if (existing is not null)
        {
            existing.Path = resolvedPath;
            existing.Name = parsedName;
            existing.Extension = parsedExtension;
            existing.Content = body.Content;
        }
        else
        {
            template.Artefacts.Add(new TemplateArtefact
            {
                Path = resolvedPath,
                Name = parsedName,
                Extension = parsedExtension,
                Content = body.Content
            });
        }

        await store.SaveAsync(template);
        var saved = template.Artefacts.First(a => MatchesArtefactRef(a, name) ||
            string.Equals(ResolveArtefactPath(a), resolvedPath, StringComparison.OrdinalIgnoreCase));
        return Ok(saved);
    }

    /// <summary>Deletes an artefact by its slug name.</summary>
    [HttpDelete("{id:guid}/artefacts/{name}")]
    public async Task<IActionResult> DeleteArtefact(Guid id, string name)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var artefact = template.Artefacts.FirstOrDefault(a => MatchesArtefactRef(a, name));
        if (artefact is null)
            return NotFound(new { error = $"Artefact '{name}' not found." });

        template.Artefacts.Remove(artefact);
        await store.SaveAsync(template);
        return NoContent();
    }

    // ── File-oriented editor API ───────────────────────────────────────────

    [HttpGet("{id:guid}/files")]
    public async Task<IActionResult> ListFiles(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var files = new List<TemplateFileDto>
        {
            new("template.report.cs", "template", template.Template, template.Mode.ToString()),
            new("data/mock.data.json", "data", JsonSerializer.Serialize(template.MockData ?? new { }, new JsonSerializerOptions { WriteIndented = true }), null)
        };

        files.AddRange(template.Artefacts.Select(a =>
        {
            var path = ResolveArtefactPath(a);
            return new TemplateFileDto(path, InferFileKind(path), a.Content, null);
        }));

        return Ok(files);
    }

    [HttpPut("{id:guid}/files")]
    public async Task<IActionResult> UpsertFile(Guid id, [FromBody] UpsertTemplateFileRequest body)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var path = NormalizeRelativePath(body.Path);

        if (string.Equals(path, "template.report.cs", StringComparison.OrdinalIgnoreCase))
        {
            template.Template = body.Content;
            if (!string.IsNullOrWhiteSpace(body.Mode) &&
                Enum.TryParse<TemplateMode>(body.Mode, ignoreCase: true, out var parsedMode))
            {
                template.Mode = parsedMode;
            }

            var savedTemplate = await store.SaveAsync(template);
            return Ok(new TemplateFileDto(path, "template", savedTemplate.Template, savedTemplate.Mode.ToString()));
        }

        if (path.EndsWith(".data.json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(path, "data/mock.data.json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                template.MockData = JsonSerializer.Deserialize<JsonElement>(body.Content);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Invalid JSON data file: {ex.Message}" });
            }

            await store.SaveAsync(template);
            return Ok(new TemplateFileDto(path, "data", body.Content, null));
        }

        var fileName = Path.GetFileName(path);
        var dotIndex = fileName.IndexOf('.');
        var name = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
        var extension = dotIndex >= 0 ? fileName[dotIndex..] : string.Empty;

        var artefact = template.Artefacts.FirstOrDefault(a =>
            string.Equals(ResolveArtefactPath(a), path, StringComparison.OrdinalIgnoreCase));

        if (artefact is null)
        {
            artefact = new TemplateArtefact
            {
                Path = path,
                Name = name,
                Extension = extension,
                Content = body.Content
            };
            template.Artefacts.Add(artefact);
        }
        else
        {
            artefact.Path = path;
            artefact.Name = name;
            artefact.Extension = extension;
            artefact.Content = body.Content;
        }

        await store.SaveAsync(template);
        return Ok(new TemplateFileDto(path, InferFileKind(path), body.Content, null));
    }

    [HttpDelete("{id:guid}/files")]
    public async Task<IActionResult> DeleteFile(Guid id, [FromQuery] string path)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        var normalizedPath = NormalizeRelativePath(path);

        if (string.Equals(normalizedPath, "template.report.cs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedPath, "data/mock.data.json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Core files cannot be deleted." });
        }

        var artefact = template.Artefacts.FirstOrDefault(a =>
            string.Equals(ResolveArtefactPath(a), normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (artefact is null)
            return NotFound(new { error = $"File '{normalizedPath}' not found." });

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

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ResolveArtefactPath(TemplateArtefact artefact)
    {
        if (!string.IsNullOrWhiteSpace(artefact.Path))
            return NormalizeRelativePath(artefact.Path);

        return NormalizeRelativePath($"{artefact.Name}{artefact.Extension}");
    }

    private static bool MatchesArtefactRef(TemplateArtefact artefact, string reference)
        => string.Equals(artefact.Name, reference, StringComparison.OrdinalIgnoreCase)
           || string.Equals($"{artefact.Name}{artefact.Extension}", reference, StringComparison.OrdinalIgnoreCase)
           || string.Equals(ResolveArtefactPath(artefact), reference, StringComparison.OrdinalIgnoreCase);

    private static string InferFileKind(string path)
    {
        if (path.EndsWith(".helpers.cs", StringComparison.OrdinalIgnoreCase)) return "helper";
        if (path.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase)) return "schema";
        if (path.EndsWith(".data.json", StringComparison.OrdinalIgnoreCase)) return "data";
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return "template";
        return "file";
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith('/'))
            normalized = normalized[1..];

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        if (segments.Any(s => s is "." or ".."))
            throw new InvalidOperationException($"Invalid relative path '{path}'.");

        return string.Join('/', segments);
    }

}

/// <summary>Request body for upserting a template artefact.</summary>
public record UpsertArtefactRequest(string Extension, string Content, string? Path = null);

public record UpsertTemplateFileRequest(string Path, string Content, string? Kind = null, string? Mode = null);

public record TemplateFileDto(string Path, string Kind, string Content, string? Mode);

