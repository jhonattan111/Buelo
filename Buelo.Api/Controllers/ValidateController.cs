using Buelo.Contracts;
using Buelo.Engine.Validators;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/validate")]
public class ValidateController(FileValidatorRegistry registry) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Validate([FromBody] FileValidateRequest request)
    {
        var result = await registry.ValidateAsync(request.Extension, request.Content);
        return Ok(result);
    }
}
