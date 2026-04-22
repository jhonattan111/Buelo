using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Renderers;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController(TemplateEngine engine, ITemplateStore store, OutputRendererRegistry renderers) : ControllerBase
{
    /// <summary>
    /// Renders a report from a C# IDocument template supplied in the request body.
    /// Use ?format=pdf (default) or ?format=excel to select output format.
    /// </summary>
    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] ReportRequest request, [FromQuery] string format = "pdf")
    {
        var renderer = renderers.TryGetRenderer(format);
        if (renderer is null)
            return BadRequest(new { error = $"Unsupported format '{format}'." });

        if (!renderer.SupportsMode(request.Mode))
            return BadRequest(new { error = $"Format '{format}' does not support mode '{request.Mode}'." });

        var input = new RendererInput
        {
            Source = request.Template,
            Mode = request.Mode,
            RawData = request.Data,
            PageSettings = request.PageSettings ?? PageSettings.Default(),
        };

        try
        {
            var bytes = await renderer.RenderAsync(input);
            var baseName = Path.GetFileNameWithoutExtension(request.FileName);
            return File(bytes, renderer.ContentType, baseName + renderer.FileExtension);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validates a C# template by compiling it with Roslyn without generating output.
    /// Always returns 200 OK; the valid field signals success or failure.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ReportValidateRequest request)
    {
        var result = await engine.ValidateAsync(request.Template, request.Mode);
        return Ok(result);
    }

    /// <summary>
    /// Renders a stored template by its GUID.
    /// Supply ?format=pdf|excel to override the template's default output format.
    /// Supply ?version=N to render from a historical snapshot.
    /// </summary>
    [HttpPost("render/{id:guid}")]
    public async Task<IActionResult> RenderById(
        Guid id,
        [FromQuery] int? version = null,
        [FromBody] TemplateRenderRequest? request = null,
        [FromQuery] string? format = null)
    {
        TemplateRecord? template;

        if (version.HasValue)
        {
            var snapshot = await store.GetVersionAsync(id, version.Value);
            if (snapshot is null)
                return NotFound(new { error = $"Version {version.Value} not found for template '{id}'." });

            var current = await store.GetAsync(id);
            if (current is null)
                return NotFound(new { error = $"Template '{id}' not found." });

            template = new TemplateRecord
            {
                Id = current.Id,
                Name = current.Name,
                Mode = current.Mode,
                OutputFormat = current.OutputFormat,
                PageSettings = current.PageSettings,
                DefaultFileName = current.DefaultFileName,
                MockData = current.MockData,
                Template = snapshot.Template,
                Artefacts = snapshot.Artefacts
            };
        }
        else
        {
            template = await store.GetAsync(id);
            if (template is null)
                return NotFound(new { error = $"Template '{id}' not found." });
        }

        var effectiveFormat = !string.IsNullOrWhiteSpace(format)
            ? format!
            : (template.OutputFormat == OutputFormat.Excel ? "excel" : "pdf");

        var renderer = renderers.TryGetRenderer(effectiveFormat);
        if (renderer is null)
            return BadRequest(new { error = $"Unsupported format '{effectiveFormat}'." });

        if (!renderer.SupportsMode(template.Mode))
            return BadRequest(new { error = $"Format '{effectiveFormat}' does not support mode '{template.Mode}'." });

        var data = request?.Data ?? template.MockData;
        if (data is null)
            return BadRequest(new { error = "No data provided and the template has no mock data configured." });

        var fileName = request?.FileName ?? template.DefaultFileName;
        var pageSettings = request?.PageSettings ?? template.PageSettings;

        try
        {
            var input = new RendererInput
            {
                Source = template.Template,
                Mode = template.Mode,
                RawData = data,
                PageSettings = pageSettings ?? PageSettings.Default(),
            };

            var bytes = await renderer.RenderAsync(input);
            return File(bytes, renderer.ContentType, Path.GetFileNameWithoutExtension(fileName) + renderer.FileExtension);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Renders a stored template using its built-in mock data.
    /// </summary>
    [HttpPost("preview/{id:guid}")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        if (template.MockData is null)
            return BadRequest(new { error = "Template has no mock data configured." });

        try
        {
            var pdf = await engine.RenderTemplateAsync(template, template.MockData, template.PageSettings);
            return File(pdf, "application/pdf", template.DefaultFileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns the list of supported output formats and their MIME types.
    /// </summary>
    [HttpGet("formats")]
    public IActionResult GetFormats()
    {
        var formats = renderers.SupportedFormats
            .Select(f =>
            {
                var r = renderers.TryGetRenderer(f)!;
                return new { format = r.Format, contentType = r.ContentType, fileExtension = r.FileExtension };
            });
        return Ok(formats);
    }
}
