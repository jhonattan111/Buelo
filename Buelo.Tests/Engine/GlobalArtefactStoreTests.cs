using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class GlobalArtefactStoreTests
{
    private static GlobalArtefact MakeArtefact(string name, string ext, string content = "{}") => new()
    {
        Id = Guid.Empty,
        Name = name,
        Extension = ext,
        Content = content,
        Tags = ["test"]
    };

    // ── InMemoryGlobalArtefactStore ───────────────────────────────────────────

    [Fact]
    public async Task InMemory_SaveAndRetrieve_ById_ReturnsArtefact()
    {
        var store = new InMemoryGlobalArtefactStore();
        var saved = await store.SaveAsync(MakeArtefact("colaborador", ".json"));

        Assert.NotEqual(Guid.Empty, saved.Id);
        var loaded = await store.GetAsync(saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal("colaborador", loaded!.Name);
        Assert.Equal(".json", loaded.Extension);
    }

    [Fact]
    public async Task InMemory_SaveAndRetrieve_ByName_ReturnsArtefact_CaseInsensitive()
    {
        var store = new InMemoryGlobalArtefactStore();
        await store.SaveAsync(MakeArtefact("Colaborador", ".JSON"));

        var loaded = await store.GetByNameAsync("colaborador", ".json");
        Assert.NotNull(loaded);
        Assert.Equal("Colaborador", loaded!.Name);
    }

    [Fact]
    public async Task InMemory_ListWithExtensionFilter_ReturnsOnlyMatchingType()
    {
        var store = new InMemoryGlobalArtefactStore();
        await store.SaveAsync(MakeArtefact("data1", ".json"));
        await store.SaveAsync(MakeArtefact("helper1", ".csx"));
        await store.SaveAsync(MakeArtefact("data2", ".json"));

        var jsonArtefacts = await store.ListAsync(".json");
        Assert.Equal(2, jsonArtefacts.Count);
        Assert.All(jsonArtefacts, a => Assert.Equal(".json", a.Extension));
    }

    [Fact]
    public async Task InMemory_Delete_ExistingArtefact_ReturnsTrue()
    {
        var store = new InMemoryGlobalArtefactStore();
        var saved = await store.SaveAsync(MakeArtefact("colaborador", ".json"));

        var deleted = await store.DeleteAsync(saved.Id);
        Assert.True(deleted);
        Assert.Null(await store.GetAsync(saved.Id));
    }

    [Fact]
    public async Task InMemory_Delete_NonExistent_ReturnsFalse()
    {
        var store = new InMemoryGlobalArtefactStore();
        var result = await store.DeleteAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task InMemory_Save_AssignsCreatedAtAndUpdatedAt()
    {
        var store = new InMemoryGlobalArtefactStore();
        var before = DateTimeOffset.UtcNow;
        var saved = await store.SaveAsync(MakeArtefact("a", ".json"));

        Assert.True(saved.CreatedAt >= before);
        Assert.True(saved.UpdatedAt >= saved.CreatedAt);
    }

    [Fact]
    public async Task InMemory_ListAll_ReturnsAllWhenNoFilter()
    {
        var store = new InMemoryGlobalArtefactStore();
        await store.SaveAsync(MakeArtefact("a", ".json"));
        await store.SaveAsync(MakeArtefact("b", ".csx"));
        await store.SaveAsync(MakeArtefact("c", ".buelo"));

        var all = await store.ListAsync();
        Assert.Equal(3, all.Count);
    }

    // ── FileSystemGlobalArtefactStore ─────────────────────────────────────────

    [Fact]
    public async Task FileSystem_SaveAndRetrieve_ById_ReturnsArtefact()
    {
        using var dir = new TempDirectory();
        var store = new FileSystemGlobalArtefactStore(dir.Path);

        var saved = await store.SaveAsync(MakeArtefact("colaborador", ".json", "{\"nome\":\"João\"}"));
        Assert.NotEqual(Guid.Empty, saved.Id);

        var loaded = await store.GetAsync(saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal("colaborador", loaded!.Name);
        Assert.Equal("{\"nome\":\"João\"}", loaded.Content);
    }

    [Fact]
    public async Task FileSystem_SaveAndRetrieve_ByName_ReturnsArtefact_CaseInsensitive()
    {
        using var dir = new TempDirectory();
        var store = new FileSystemGlobalArtefactStore(dir.Path);
        await store.SaveAsync(MakeArtefact("Formatters", ".csx", "string Format(int v) => v.ToString();"));

        var loaded = await store.GetByNameAsync("formatters", ".csx");
        Assert.NotNull(loaded);
        Assert.Contains("Format", loaded!.Content);
    }

    [Fact]
    public async Task FileSystem_ListWithExtensionFilter_ReturnsOnlyMatchingType()
    {
        using var dir = new TempDirectory();
        var store = new FileSystemGlobalArtefactStore(dir.Path);
        await store.SaveAsync(MakeArtefact("d1", ".json"));
        await store.SaveAsync(MakeArtefact("h1", ".csx"));
        await store.SaveAsync(MakeArtefact("d2", ".json"));

        var jsonArtefacts = await store.ListAsync(".json");
        Assert.Equal(2, jsonArtefacts.Count);
        Assert.All(jsonArtefacts, a => Assert.Equal(".json", a.Extension));
    }

    [Fact]
    public async Task FileSystem_Delete_ExistingArtefact_ReturnsTrue()
    {
        using var dir = new TempDirectory();
        var store = new FileSystemGlobalArtefactStore(dir.Path);
        var saved = await store.SaveAsync(MakeArtefact("colaborador", ".json"));

        var deleted = await store.DeleteAsync(saved.Id);
        Assert.True(deleted);
        Assert.Null(await store.GetAsync(saved.Id));
    }

    [Fact]
    public async Task FileSystem_Delete_NonExistent_ReturnsFalse()
    {
        using var dir = new TempDirectory();
        var store = new FileSystemGlobalArtefactStore(dir.Path);

        var result = await store.DeleteAsync(Guid.NewGuid());
        Assert.False(result);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
