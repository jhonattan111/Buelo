using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class FileSystemTemplateStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemTemplateStore _store;

    public FileSystemTemplateStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"buelo-tests-{Guid.NewGuid()}");
        _store = new FileSystemTemplateStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ── SaveAsync / GetAsync round-trip ──────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NewTemplate_AssignsIdAndPersists()
    {
        var template = BuildTemplate();

        var saved = await _store.SaveAsync(template);

        Assert.NotEqual(Guid.Empty, saved.Id);
        var retrieved = await _store.GetAsync(saved.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(saved.Id, retrieved.Id);
        Assert.Equal("Test Template", retrieved.Name);
        Assert.Equal("page.Content().Text(\"hello\");", retrieved.Template);
        Assert.Equal(TemplateMode.Sections, retrieved.Mode);
    }

    [Fact]
    public async Task SaveAsync_RoundTrip_PreservesAllScalarFields()
    {
        var original = BuildTemplate();
        original.Description = "A description";
        original.DefaultFileName = "my-report.pdf";
        original.PageSettings = new PageSettings { PageSize = "Letter", MarginHorizontal = 1.5f };
        original.DataSchema = "{ \"type\": \"object\" }";

        var saved = await _store.SaveAsync(original);
        var retrieved = await _store.GetAsync(saved.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("A description", retrieved.Description);
        Assert.Equal("my-report.pdf", retrieved.DefaultFileName);
        Assert.Equal("Letter", retrieved.PageSettings.PageSize);
        Assert.Equal(1.5f, retrieved.PageSettings.MarginHorizontal);
        Assert.Equal("{ \"type\": \"object\" }", retrieved.DataSchema);
    }

    // ── Artefact persistence ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_WithArtefacts_PersistsAndReloads()
    {
        var template = BuildTemplate();
        template.Artefacts.Add(new TemplateArtefact
        {
            Name = "mockdata",
            Extension = ".json",
            Content = """{"name":"Alice"}"""
        });
        template.Artefacts.Add(new TemplateArtefact
        {
            Name = "helper-tax",
            Extension = ".cs",
            Content = "// helper"
        });

        var saved = await _store.SaveAsync(template);
        var retrieved = await _store.GetAsync(saved.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.Artefacts.Count);

        var mockdata = retrieved.Artefacts.First(a => a.Name == "mockdata");
        Assert.Equal(".json", mockdata.Extension);
        Assert.Equal("""{"name":"Alice"}""", mockdata.Content);

        var helper = retrieved.Artefacts.First(a => a.Name == "helper-tax");
        Assert.Equal(".cs", helper.Extension);
        Assert.Equal("// helper", helper.Content);
    }

    [Fact]
    public async Task SaveAsync_UpsertArtefact_UpdatesExistingContent()
    {
        var template = BuildTemplate();
        template.Artefacts.Add(new TemplateArtefact
        {
            Name = "mockdata",
            Extension = ".json",
            Content = """{"name":"Old"}"""
        });
        var saved = await _store.SaveAsync(template);

        // Update the artefact content and save again.
        var retrieved = await _store.GetAsync(saved.Id);
        var existing = retrieved!.Artefacts.First(a => a.Name == "mockdata");
        existing.Content = """{"name":"New"}""";
        await _store.SaveAsync(retrieved);

        var updated = await _store.GetAsync(saved.Id);
        Assert.NotNull(updated);
        var artefact = updated.Artefacts.Single();
        Assert.Equal("""{"name":"New"}""", artefact.Content);
    }

    [Fact]
    public async Task SaveAsync_RemoveArtefact_DeletesFileFromDisk()
    {
        var template = BuildTemplate();
        template.Artefacts.Add(new TemplateArtefact { Name = "todelete", Extension = ".json", Content = "{}" });
        var saved = await _store.SaveAsync(template);

        // Verify the file exists.
        var artefactFile = Path.Combine(_root, saved.Id.ToString(), "todelete.json");
        Assert.True(File.Exists(artefactFile));

        // Remove artefact and save again.
        var retrieved = await _store.GetAsync(saved.Id);
        retrieved!.Artefacts.Clear();
        await _store.SaveAsync(retrieved);

        Assert.False(File.Exists(artefactFile));
    }

    [Fact]
    public async Task SaveAsync_WithNestedArtefactPath_PersistsAndReloadsPath()
    {
        var template = BuildTemplate();
        template.Artefacts.Add(new TemplateArtefact
        {
            Path = "helpers/tax/calc.helpers.cs",
            Name = "calc",
            Extension = ".helpers.cs",
            Content = "// helper"
        });

        var saved = await _store.SaveAsync(template);
        var retrieved = await _store.GetAsync(saved.Id);

        Assert.NotNull(retrieved);
        var artefact = Assert.Single(retrieved.Artefacts);
        Assert.Equal("helpers/tax/calc.helpers.cs", artefact.Path);
        Assert.Equal("// helper", artefact.Content);

        var artefactFile = Path.Combine(_root, saved.Id.ToString(), "helpers", "tax", "calc.helpers.cs");
        Assert.True(File.Exists(artefactFile));
    }

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsAllSavedTemplates()
    {
        var t1 = await _store.SaveAsync(BuildTemplate("Alpha"));
        var t2 = await _store.SaveAsync(BuildTemplate("Beta"));
        var t3 = await _store.SaveAsync(BuildTemplate("Gamma"));

        var all = (await _store.ListAsync()).ToList();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, t => t.Id == t1.Id);
        Assert.Contains(all, t => t.Id == t2.Id);
        Assert.Contains(all, t => t.Id == t3.Id);
    }

    [Fact]
    public async Task ListAsync_EmptyStore_ReturnsEmptyCollection()
    {
        var all = await _store.ListAsync();
        Assert.Empty(all);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingTemplate_RemovesDirectory()
    {
        var saved = await _store.SaveAsync(BuildTemplate());
        var dir = Path.Combine(_root, saved.Id.ToString());
        Assert.True(Directory.Exists(dir));

        var result = await _store.DeleteAsync(saved.Id);

        Assert.True(result);
        Assert.False(Directory.Exists(dir));
        Assert.Null(await _store.GetAsync(saved.Id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentTemplate_ReturnsFalse()
    {
        var result = await _store.DeleteAsync(Guid.NewGuid());
        Assert.False(result);
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_NonExistentId_ReturnsNull()
    {
        var result = await _store.GetAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TemplateRecord BuildTemplate(string name = "Test Template") => new()
    {
        Name = name,
        Template = "page.Content().Text(\"hello\");",
        Mode = TemplateMode.Sections
    };
}
