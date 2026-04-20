using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class WorkspaceControllerTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceController _controller;

    public WorkspaceControllerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"buelo-workspace-api-{Guid.NewGuid()}");
        var store = new FileSystemWorkspaceStore(_root);
        _controller = new WorkspaceController(store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task CreateFolderAndFile_ThenGetTree_ReturnsNodes()
    {
        var createFolder = await _controller.CreateFolder(new CreateFolderRequest("reports"));
        Assert.IsType<NoContentResult>(createFolder);

        var createFile = await _controller.CreateFile(new CreateFileRequest("reports/main.buelo", "report title:\n  text: Hi"));
        Assert.IsType<OkObjectResult>(createFile);

        var treeResult = await _controller.GetTree();
        var ok = Assert.IsType<OkObjectResult>(treeResult);
        var nodes = Assert.IsAssignableFrom<IEnumerable<WorkspaceNode>>(ok.Value);
        Assert.NotEmpty(nodes);
    }

    [Fact]
    public async Task DeleteNode_RemovesFile()
    {
        await _controller.CreateFolder(new CreateFolderRequest("data"));
        await _controller.CreateFile(new CreateFileRequest("data/mock.json", "{}"));

        var deleted = await _controller.DeleteNode("data/mock.json");
        Assert.IsType<NoContentResult>(deleted);

        var missing = await _controller.GetFileContent("data/mock.json");
        Assert.IsType<NotFoundObjectResult>(missing);
    }
}
