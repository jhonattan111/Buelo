using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/workspace")]
public class WorkspaceController(IWorkspaceStore store) : ControllerBase
{
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var tree = await store.GetTreeAsync();
        return Ok(tree);
    }

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
    {
        try
        {
            await store.CreateFolderAsync(request.Path);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("files")]
    public async Task<IActionResult> CreateFile([FromBody] CreateFileRequest request)
    {
        try
        {
            var created = await store.CreateFileAsync(request.Path, request.Content ?? string.Empty, request.Overwrite);
            return Ok(created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("files/content")]
    public async Task<IActionResult> GetFileContent([FromQuery] string path)
    {
        var file = await store.GetFileAsync(path);
        if (file is null)
            return NotFound(new { error = $"File '{path}' not found." });

        return Ok(file);
    }

    [HttpPut("files/content")]
    public async Task<IActionResult> SaveFileContent([FromBody] SaveFileRequest request)
    {
        try
        {
            var saved = await store.UpdateFileAsync(request.Path, request.Content, request.CreateIfMissing);
            return Ok(saved);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("files/move")]
    public async Task<IActionResult> MoveFile([FromBody] MoveNodeRequest request)
    {
        try
        {
            await store.MoveAsync(request.Path, request.DestinationPath, request.Overwrite);
            return NoContent();
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("files/rename")]
    public async Task<IActionResult> RenameFile([FromBody] RenameNodeRequest request)
    {
        try
        {
            await store.RenameAsync(request.Path, request.NewName, request.Overwrite);
            return NoContent();
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("nodes")]
    public async Task<IActionResult> DeleteNode([FromQuery] string path)
    {
        try
        {
            await store.DeleteAsync(path, recursive: true);
            return NoContent();
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record CreateFolderRequest(string Path);
public record CreateFileRequest(string Path, string? Content = null, bool Overwrite = false);
public record SaveFileRequest(string Path, string Content, bool CreateIfMissing = false);
public record MoveNodeRequest(string Path, string DestinationPath, bool Overwrite = false);
public record RenameNodeRequest(string Path, string NewName, bool Overwrite = false);
