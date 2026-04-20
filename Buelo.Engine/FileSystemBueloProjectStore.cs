using Buelo.Contracts;
using System.Text.Json;

namespace Buelo.Engine;

public class FileSystemBueloProjectStore : IBueloProjectStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public FileSystemBueloProjectStore(string rootPath)
    {
        _filePath = Path.Combine(rootPath, "project.bueloproject");
    }

    public async Task<BueloProject> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return new BueloProject();

            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<BueloProject>(json, _jsonOptions) ?? new BueloProject();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BueloProject> SaveAsync(BueloProject project)
    {
        await _lock.WaitAsync();
        try
        {
            project.UpdatedAt = DateTimeOffset.UtcNow;
            if (project.CreatedAt == default)
                project.CreatedAt = project.UpdatedAt;

            var dir = Path.GetDirectoryName(_filePath)!;
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmpPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(project, _jsonOptions);
            await File.WriteAllTextAsync(tmpPath, json);
            File.Move(tmpPath, _filePath, overwrite: true);
            return project;
        }
        finally
        {
            _lock.Release();
        }
    }
}
