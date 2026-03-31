using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController(TemplateEngine engine) : ControllerBase
{
    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] ReportRequest request)
    {
        var pdf = await engine.RenderAsync(request.Template, request.Data);

        return File(pdf, "application/pdf", request.FileName);
    }
}
