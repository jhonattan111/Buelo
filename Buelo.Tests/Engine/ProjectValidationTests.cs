using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Validators;

namespace Buelo.Tests.Engine;

public class ProjectValidationTests
{
    [Fact]
    public async Task Workspace_WithOneInvalidJson_ReturnsInvalidAggregate()
    {
        var root = CreateTempWorkspace();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "reports"));
            Directory.CreateDirectory(Path.Combine(root, "data"));

            await File.WriteAllTextAsync(Path.Combine(root, "reports", "relatorio.buelo"), "report title:\n  text: Hello");
            await File.WriteAllTextAsync(Path.Combine(root, "data", "colaboradores.json"), "{ invalid }");

            var enumerator = new FileSystemWorkspaceFileEnumerator(root);
            var registry = CreateRegistry();
            var result = await ValidateProjectAsync(enumerator, registry);

            Assert.Equal(2, result.Files.Count);
            Assert.False(result.Valid);
            Assert.True(result.TotalErrors > 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Workspace_WithAllValidFiles_ReturnsValidAggregate()
    {
        var root = CreateTempWorkspace();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "reports"));
            Directory.CreateDirectory(Path.Combine(root, "data"));

            await File.WriteAllTextAsync(Path.Combine(root, "reports", "relatorio.buelo"), "report title:\n  text: Hello");
            await File.WriteAllTextAsync(Path.Combine(root, "data", "colaboradores.json"), "{\"ok\": true}");

            var enumerator = new FileSystemWorkspaceFileEnumerator(root);
            var registry = CreateRegistry();
            var result = await ValidateProjectAsync(enumerator, registry);

            Assert.True(result.Valid);
            Assert.Equal(0, result.TotalErrors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Workspace_ResultFiles_AreOrderedByPath()
    {
        var root = CreateTempWorkspace();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "z"));
            Directory.CreateDirectory(Path.Combine(root, "a"));

            await File.WriteAllTextAsync(Path.Combine(root, "z", "x.json"), "{\"v\": 1}");
            await File.WriteAllTextAsync(Path.Combine(root, "a", "x.buelo"), "report title:\n  text: Hello");

            var enumerator = new FileSystemWorkspaceFileEnumerator(root);
            var registry = CreateRegistry();
            var result = await ValidateProjectAsync(enumerator, registry);

            Assert.Equal(2, result.Files.Count);
            Assert.Equal("a/x.buelo", result.Files[0].Path);
            Assert.Equal("z/x.json", result.Files[1].Path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<ProjectValidationResult> ValidateProjectAsync(IWorkspaceFileEnumerator enumerator, FileValidatorRegistry registry)
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
        return result;
    }

    private static FileValidatorRegistry CreateRegistry() => new(
    [
        new BueloDslValidator(),
        new JsonFileValidator(),
        new CsharpFileValidator()
    ]);

    private static string CreateTempWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"buelo-project-validation-{Guid.NewGuid()}");
        Directory.CreateDirectory(root);
        return root;
    }
}
