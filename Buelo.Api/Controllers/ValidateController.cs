using Buelo.Contracts;
using Buelo.Engine.Validators;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/validate")]
public class ValidateController(FileValidatorRegistry registry, IWorkspaceFileEnumerator enumerator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Validate([FromBody] FileValidateRequest request)
    {
        var result = await registry.ValidateAsync(request.Extension, request.Content);
        return Ok(result);
    }

    [HttpPost("project")]
    public async Task<IActionResult> ValidateProject()
    {
        var result = new ProjectValidationResult();

        await foreach (var file in enumerator.EnumerateAsync())
        {
            var fileResult = await registry.ValidateAsync(file.Extension, file.Content);
            result.Files.Add(new FileValidationEntry
            {
                Path = file.RelativePath,
                Extension = file.Extension,
                Result = fileResult
            });
        }

        result.Files = result.Files.OrderBy(f => f.Path).ToList();
        return Ok(result);
    }
}
