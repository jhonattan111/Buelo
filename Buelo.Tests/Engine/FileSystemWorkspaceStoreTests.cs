using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class FileSystemWorkspaceStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemWorkspaceStore _store;

    public FileSystemWorkspaceStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"buelo-workspace-tests-{Guid.NewGuid()}");
        _store = new FileSystemWorkspaceStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task CreateFolderAndFile_ShouldPersistAndListInTree()
    {
        await _store.CreateFolderAsync("reports/monthly");
        var file = await _store.CreateFileAsync("reports/monthly/main.buelo", "report title:\n  text: Hello");

        Assert.Equal("reports/monthly/main.buelo", file.Path);

        var tree = await _store.GetTreeAsync();
        Assert.Contains(tree, n => n.Type == "folder" && n.Path == "reports");

        var files = await _store.ListFilesAsync(".buelo");
        Assert.Contains(files, f => f.Path == "reports/monthly/main.buelo");
    }

    [Fact]
    public async Task NormalizePath_ShouldBlockTraversal()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.CreateFileAsync("../outside.json", "{}"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.CreateFolderAsync("a/../../b"));
    }
}
