using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class BueloProjectStoreTests
{
    // ── InMemoryBueloProjectStore ─────────────────────────────────────────────

    [Fact]
    public async Task InMemory_GetAsync_WhenUninitialized_ReturnsDefaults()
    {
        var store = new InMemoryBueloProjectStore();

        var project = await store.GetAsync();

        Assert.NotNull(project);
        Assert.Equal("Buelo Project", project.Name);
        Assert.Equal("1.0.0", project.Version);
        Assert.Equal("pdf", project.DefaultOutputFormat);
        Assert.NotNull(project.PageSettings);
    }

    [Fact]
    public async Task InMemory_SaveAndGet_PersistsAllFields()
    {
        var store = new InMemoryBueloProjectStore();

        var input = new BueloProject
        {
            Name = "ACME Reports",
            Description = "HR reports",
            Version = "2.0.0",
            DefaultOutputFormat = "excel",
            PageSettings = new PageSettings { PageSize = "Letter" }
        };

        var saved = await store.SaveAsync(input);
        var loaded = await store.GetAsync();

        Assert.Equal("ACME Reports", loaded.Name);
        Assert.Equal("HR reports", loaded.Description);
        Assert.Equal("2.0.0", loaded.Version);
        Assert.Equal("excel", loaded.DefaultOutputFormat);
        Assert.Equal("Letter", loaded.PageSettings.PageSize);
    }

    [Fact]
    public async Task InMemory_SaveAsync_UpdatesTimestamp()
    {
        var store = new InMemoryBueloProjectStore();

        var saved = await store.SaveAsync(new BueloProject { Name = "Test" });

        Assert.NotEqual(default, saved.UpdatedAt);
        Assert.NotEqual(default, saved.CreatedAt);
    }

    // ── FileSystemBueloProjectStore ───────────────────────────────────────────

    [Fact]
    public async Task FileSystem_GetAsync_WhenNoFileExists_ReturnsDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), $"buelo-proj-{Guid.NewGuid()}");
        try
        {
            var store = new FileSystemBueloProjectStore(root);
            var project = await store.GetAsync();

            Assert.NotNull(project);
            Assert.Equal("Buelo Project", project.Name);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileSystem_SaveAndGet_PersistsAllFields()
    {
        var root = Path.Combine(Path.GetTempPath(), $"buelo-proj-{Guid.NewGuid()}");
        try
        {
            var store = new FileSystemBueloProjectStore(root);

            var input = new BueloProject
            {
                Name = "FileSystem Project",
                Description = "Persisted",
                Version = "3.0.0",
                PageSettings = new PageSettings { PageSize = "A3" }
            };

            await store.SaveAsync(input);
            var loaded = await store.GetAsync();

            Assert.Equal("FileSystem Project", loaded.Name);
            Assert.Equal("Persisted", loaded.Description);
            Assert.Equal("3.0.0", loaded.Version);
            Assert.Equal("A3", loaded.PageSettings.PageSize);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileSystem_SaveAsync_UpdatesTimestamp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"buelo-proj-{Guid.NewGuid()}");
        try
        {
            var store = new FileSystemBueloProjectStore(root);

            var saved = await store.SaveAsync(new BueloProject { Name = "Ts" });

            Assert.NotEqual(default, saved.UpdatedAt);
            Assert.NotEqual(default, saved.CreatedAt);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
