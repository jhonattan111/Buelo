using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.BueloDsl;
using Buelo.Engine.Renderers;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController(TemplateEngine engine, ITemplateStore store, OutputRendererRegistry renderers) : ControllerBase
{
    /// <summary>
    /// Renders a report from a template supplied directly in the request body.
    /// Use the optional <c>?format=pdf</c> (default) or <c>?format=excel</c> query parameter
    /// to select the output format.
    /// </summary>
    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] ReportRequest request, [FromQuery] string format = "pdf")
    {
        var renderer = renderers.TryGetRenderer(format);
        if (renderer is null)
            return BadRequest(new { error = $"Unsupported format '{format}'." });

        var effectiveMode = BueloDslParser.IsBueloDslSource(request.Template) && request.Mode == TemplateMode.Sections
            ? TemplateMode.BueloDsl
            : request.Mode;

        if (!renderer.SupportsMode(effectiveMode))
            return BadRequest(new { error = $"Format '{format}' does not support template mode '{effectiveMode}'." });

        BueloDslDocument? ast = effectiveMode == TemplateMode.BueloDsl
            ? BueloDslParser.Parse(request.Template) : null;

        var input = new RendererInput
        {
            Source = request.Template,
            Mode = effectiveMode,
            RawData = request.Data,
            PageSettings = request.PageSettings ?? PageSettings.Default(),
            BueloDslDocument = ast,
            FormatHints = ast?.Directives.FormatHints != null
                ? new Dictionary<string, string>(ast.Directives.FormatHints)
                : new Dictionary<string, string>()
        };

        var bytes = await renderer.RenderAsync(input);
        var baseName = Path.GetFileNameWithoutExtension(request.FileName);
        return File(bytes, renderer.ContentType, baseName + renderer.FileExtension);
    }

    /// <summary>
    /// Validates a template by compiling it without generating a PDF.
    /// Always returns <c>200 OK</c>; the <c>valid</c> field signals success or failure.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ReportValidateRequest request)
    {
        var result = await engine.ValidateAsync(request.Template, request.Mode);
        return Ok(result);
    }

    /// <summary>
    /// Renders a previously saved template by its GUID.
    /// The request body is optional: omit it to fall back to the template's mock data and settings.
    /// Supply <c>?version=N</c> to render from a historical snapshot instead of the current template.
    /// Supply <c>?format=pdf</c> (default) or <c>?format=excel</c> to choose the output format.
    /// </summary>
    [HttpPost("render/{id:guid}")]
    public async Task<IActionResult> RenderById(Guid id, [FromQuery] int? version = null, [FromBody] TemplateRenderRequest? request = null, [FromQuery] string format = "pdf")
    {
        var renderer = renderers.TryGetRenderer(format);
        if (renderer is null)
            return BadRequest(new { error = $"Unsupported format '{format}'." });

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

        var data = request?.Data ?? template.MockData;
        if (data is null)
            return BadRequest(new { error = "No data provided and the template has no mock data configured." });

        var fileName = request?.FileName ?? template.DefaultFileName;
        var pageSettings = request?.PageSettings ?? template.PageSettings;

        var pdf = await engine.RenderTemplateAsync(template, data, pageSettings);
        return File(pdf, renderer.ContentType, Path.GetFileNameWithoutExtension(fileName) + renderer.FileExtension);
    }

    /// <summary>
    /// Renders a previously saved template using its built-in mock data.
    /// Useful for quickly previewing the template without supplying real data.
    /// </summary>
    [HttpPost("preview/{id:guid}")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        if (template.MockData is null)
            return BadRequest(new { error = "Template has no mock data configured. Add MockData to the template to enable preview." });

        var pdf = await engine.RenderTemplateAsync(template, template.MockData, template.PageSettings);
        return File(pdf, "application/pdf", template.DefaultFileName);
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


