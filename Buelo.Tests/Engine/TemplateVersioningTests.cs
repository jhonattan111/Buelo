using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

/// <summary>
/// Tests for template versioning in both <see cref="InMemoryTemplateStore"/>
/// and <see cref="FileSystemTemplateStore"/>.
/// </summary>
public class TemplateVersioningTests : IDisposable
{
    private readonly string _fsRoot;

    public TemplateVersioningTests()
    {
        _fsRoot = Path.Combine(Path.GetTempPath(), $"buelo-version-tests-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_fsRoot))
            Directory.Delete(_fsRoot, recursive: true);
    }

    // â”€â”€ InMemoryTemplateStore â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task InMemory_FirstSave_CreatesNoVersions()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(Build("v1"));

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task InMemory_SecondSave_CreatesVersion1()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(Build("Source v1"));

        saved.Template = "Source v2";
        await store.SaveAsync(saved);

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Single(versions);
        Assert.Equal(1, versions[0].Version);
        Assert.Equal("Source v1", versions[0].Template);
    }

    [Fact]
    public async Task InMemory_ThreeSaves_CreatesTwo_Versions()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(Build("v1"));

        saved.Template = "v2";
        await store.SaveAsync(saved);
        saved.Template = "v3";
        await store.SaveAsync(saved);

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Equal(2, versions.Count);
        Assert.Equal("v1", versions[0].Template);
        Assert.Equal("v2", versions[1].Template);
    }

    [Fact]
    public async Task InMemory_GetVersionAsync_ReturnsCorrectSnapshot()
    {
        var store = new InMemoryTemplateStore();
        var template = Build("original");
        template.Artefacts.Add(new TemplateArtefact { Name = "data", Extension = ".json", Content = "{}" });
        var saved = await store.SaveAsync(template);

        saved.Template = "updated";
        saved.Artefacts.Clear();
        await store.SaveAsync(saved);

        var v1 = await store.GetVersionAsync(saved.Id, 1);
        Assert.NotNull(v1);
        Assert.Equal("original", v1.Template);
        Assert.Single(v1.Artefacts);
        Assert.Equal("data", v1.Artefacts[0].Name);
    }

    [Fact]
    public async Task InMemory_GetVersionAsync_UnknownVersion_ReturnsNull()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(Build("x"));

        var result = await store.GetVersionAsync(saved.Id, 99);
        Assert.Null(result);
    }

    [Fact]
    public async Task InMemory_MaxVersions_OldestVersionEvicted()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(Build("v0"));

        // Trigger MaxVersionsPerTemplate + 1 additional saves to overflow.
        for (int i = 1; i <= InMemoryTemplateStore.MaxVersionsPerTemplate + 1; i++)
        {
            saved.Template = $"v{i}";
            await store.SaveAsync(saved);
        }

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Equal(InMemoryTemplateStore.MaxVersionsPerTemplate, versions.Count);
        // Version 1 (oldest, v0) should have been evicted; earliest remaining should be v1.
        Assert.DoesNotContain(versions, v => v.Template == "v0");
    }

    [Fact]
    public async Task InMemory_DeleteAsync_ClearsVersionHistory()
    {
        var store = new InMemoryTemplateStore();
        var saved = await store.SaveAsync(Build("A"));
        saved.Template = "B";
        await store.SaveAsync(saved);

        await store.DeleteAsync(saved.Id);
        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Empty(versions);
    }

    // â”€â”€ FileSystemTemplateStore â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task FileSystem_FirstSave_NoVersionDirectory()
    {
        var store = new FileSystemTemplateStore(_fsRoot);
        var saved = await store.SaveAsync(Build("first"));

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task FileSystem_SecondSave_WritesSnapshotFile()
    {
        var store = new FileSystemTemplateStore(_fsRoot);
        var saved = await store.SaveAsync(Build("src_v1"));

        saved.Template = "src_v2";
        await store.SaveAsync(saved);

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Single(versions);
        Assert.Equal("src_v1", versions[0].Template);
    }

    [Fact]
    public async Task FileSystem_RoundTrip_VersionPreservesArtefacts()
    {
        var store = new FileSystemTemplateStore(_fsRoot);
        var template = Build("original");
        template.Artefacts.Add(new TemplateArtefact { Name = "mock", Extension = ".json", Content = "{\"x\":1}" });
        var saved = await store.SaveAsync(template);

        saved.Template = "updated";
        saved.Artefacts.Clear();
        await store.SaveAsync(saved);

        var v1 = await store.GetVersionAsync(saved.Id, 1);
        Assert.NotNull(v1);
        Assert.Equal("original", v1.Template);
        Assert.Single(v1.Artefacts);
        Assert.Equal("{\"x\":1}", v1.Artefacts[0].Content);
    }

    [Fact]
    public async Task FileSystem_GetVersionAsync_NonExistent_ReturnsNull()
    {
        var store = new FileSystemTemplateStore(_fsRoot);
        var saved = await store.SaveAsync(Build("x"));

        var result = await store.GetVersionAsync(saved.Id, 99);
        Assert.Null(result);
    }

    // â”€â”€ Restore via API-layer simulation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task InMemory_RestoreVersion_RewindsTemplateAndCreatesNewVersion()
    {
        var store = new InMemoryTemplateStore();

        // v0: initial save.
        var template = await store.SaveAsync(Build("initial"));

        // v1: first update (saves snapshot of "initial").
        template.Template = "updated";
        await store.SaveAsync(template);

        // Simulate restore: read version 1, apply to current, save again.
        var snapshot = await store.GetVersionAsync(template.Id, 1);
        Assert.NotNull(snapshot);

        var current = await store.GetAsync(template.Id);
        current!.Template = snapshot.Template;
        await store.SaveAsync(current);

        var restored = await store.GetAsync(template.Id);
        Assert.Equal("initial", restored!.Template);

        // There should now be 2 versions (v1 = "initial" snapshot, v2 = "updated" snapshot).
        var allVersions = await store.GetVersionsAsync(template.Id);
        Assert.Equal(2, allVersions.Count);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static TemplateRecord Build(string templateSource) => new()
    {
        Name = "Versioning Test",
        Template = templateSource,
        Mode = TemplateMode.FullClass
    };
}
