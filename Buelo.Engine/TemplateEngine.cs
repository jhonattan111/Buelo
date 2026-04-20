using Buelo.Contracts;
using Buelo.Engine.BueloDsl;
using System.Dynamic;
using System.Text.Json;

namespace Buelo.Engine;

public class TemplateEngine
{
    private readonly IHelperRegistry _helpers;
    private readonly ITemplateStore? _store;
    private readonly IWorkspaceStore? _workspaceStore;

    public TemplateEngine(
        IHelperRegistry helpers,
        ITemplateStore? store = null,
        IWorkspaceStore? workspaceStore = null,
        IGlobalArtefactStore? _ = null)
    {
        _helpers = helpers;
        _store = store;
        _workspaceStore = workspaceStore;
    }

    internal static PageSettings MergeSettings(PageSettings? template, PageSettings? request)
        => request ?? template ?? PageSettings.Default();

    public async Task<byte[]> RenderAsync(string template, object data, TemplateMode mode = TemplateMode.BueloDsl, PageSettings? pageSettings = null)
    {
        var effectiveSettings = MergeSettings(PageSettings.Default(), pageSettings);
        var context = new ReportContext
        {
            Data = ConvertToDynamic(data),
            Helpers = _helpers,
            PageSettings = effectiveSettings,
            Globals = new Dictionary<string, object>
            {
                ["__pageSettings"] = effectiveSettings
            }
        };

        var engine = new BueloDslEngine(_helpers);
        return await engine.RenderAsync(template, context);
    }

    public async Task<byte[]> RenderTemplateAsync(TemplateRecord template, object? data, PageSettings? pageSettings = null)
    {
        var ast = BueloDslParser.Parse(template.Template);

        object? effectiveData = data;
        if (ast.Directives.DataRef is { } dataRef)
        {
            var artefact = template.Artefacts.FirstOrDefault(a =>
                string.Equals(a.Path, dataRef, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Name, dataRef, StringComparison.OrdinalIgnoreCase) ||
                string.Equals($"{a.Name}{a.Extension}", dataRef, StringComparison.OrdinalIgnoreCase));

            if (artefact is not null)
            {
                effectiveData = JsonSerializer.Deserialize<JsonElement>(artefact.Content);
            }
            if (effectiveData is null && _store is not null && Guid.TryParse(dataRef, out var refId))
            {
                var refTemplate = await _store.GetAsync(refId);
                effectiveData = refTemplate?.MockData;
            }
        }

        effectiveData ??= template.MockData;
        if (effectiveData is null)
            throw new InvalidOperationException(
                "No data available for rendering. Provide data in the request, configure MockData, or declare @data in .buelo directives.");

        var effectivePageSettings = template.PageSettings ?? PageSettings.Default();
        if (ast.Directives.Settings is { } ds)
            effectivePageSettings = BueloDslEngine.ApplyDslSettingsStatic(effectivePageSettings, ds);
        if (ast.Directives.ProjectConfig is { } inlineProject)
            effectivePageSettings = ApplyProjectConfigSettings(effectivePageSettings, inlineProject);

        effectivePageSettings = MergeSettings(effectivePageSettings, pageSettings);

        var context = new ReportContext
        {
            Data = ConvertToDynamic(effectiveData),
            Helpers = _helpers,
            PageSettings = effectivePageSettings,
            Globals = new Dictionary<string, object>
            {
                ["__pageSettings"] = effectivePageSettings
            }
        };

        var engine = new BueloDslEngine(_helpers);
        return engine.RenderParsed(ast, context);
    }

    public async Task<byte[]> RenderWorkspaceFileAsync(
        string templatePath,
        object? data,
        string? dataSourcePath = null,
        PageSettings? pageSettings = null)
    {
        if (_workspaceStore is null)
            throw new InvalidOperationException("Workspace rendering is not available because IWorkspaceStore is not configured.");

        var resolved = await BueloImportResolver.ResolveAsync(_workspaceStore, templatePath);
        var ast = BueloDslParser.Parse(resolved.Source);

        var effectivePageSettings = pageSettings ?? PageSettings.Default();
        if (ast.Directives.Settings is { } ds)
            effectivePageSettings = BueloDslEngine.ApplyDslSettingsStatic(effectivePageSettings, ds);
        if (ast.Directives.ProjectConfig is { } inlineProject)
            effectivePageSettings = ApplyProjectConfigSettings(effectivePageSettings, inlineProject);

        var effectiveData = data;
        if (effectiveData is null)
        {
            var boundDataPath =
                dataSourcePath
                ?? effectivePageSettings.DataSourcePath
                ?? ast.Directives.DataRef;

            if (!string.IsNullOrWhiteSpace(boundDataPath))
            {
                var resolvedDataPath = ResolveWorkspacePath(resolved.EntryPath, boundDataPath);
                var dataFile = await _workspaceStore.GetFileAsync(resolvedDataPath);
                if (dataFile is null)
                    throw new InvalidOperationException($"Bound data source '{resolvedDataPath}' was not found.");

                if (!string.Equals(dataFile.Extension, ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Bound data source '{resolvedDataPath}' must be a .json file.");

                try
                {
                    effectiveData = JsonSerializer.Deserialize<JsonElement>(dataFile.Content);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Data source '{resolvedDataPath}' contains invalid JSON: {ex.Message}");
                }
            }
        }

        effectiveData ??= new { };

        var context = new ReportContext
        {
            Data = ConvertToDynamic(effectiveData),
            Helpers = _helpers,
            PageSettings = effectivePageSettings,
            Globals = new Dictionary<string, object>
            {
                ["__pageSettings"] = effectivePageSettings
            }
        };

        var engine = new BueloDslEngine(_helpers);
        return engine.RenderParsed(ast, context);
    }

    public Task<ValidationResult> ValidateAsync(string template, TemplateMode mode = TemplateMode.BueloDsl)
    {
        BueloDslParser.Parse(template, out var errors);

        var parseErrors = errors
            .Where(e => e.Severity == BueloDslErrorSeverity.Error)
            .Select(e => new ValidationError(e.Message, e.Line, e.Column))
            .ToList();

        return Task.FromResult(new ValidationResult
        {
            Valid = parseErrors.Count == 0,
            Errors = parseErrors
        });
    }

    internal static PageSettings ApplyProjectConfigSettings(PageSettings @base, BueloDslProjectConfig projectConfig)
    {
        return new PageSettings
        {
            DataSourcePath = projectConfig.DataSourcePath ?? @base.DataSourcePath,
            PageSize = projectConfig.PageSize ?? @base.PageSize,
            MarginHorizontal = projectConfig.MarginHorizontal.HasValue ? (float)projectConfig.MarginHorizontal.Value : @base.MarginHorizontal,
            MarginVertical = projectConfig.MarginVertical.HasValue ? (float)projectConfig.MarginVertical.Value : @base.MarginVertical,
            BackgroundColor = projectConfig.BackgroundColor ?? @base.BackgroundColor,
            WatermarkText = projectConfig.WatermarkText ?? @base.WatermarkText,
            WatermarkColor = @base.WatermarkColor,
            WatermarkOpacity = @base.WatermarkOpacity,
            WatermarkFontSize = @base.WatermarkFontSize,
            DefaultFontSize = projectConfig.DefaultFontSize ?? @base.DefaultFontSize,
            DefaultTextColor = projectConfig.DefaultTextColor ?? @base.DefaultTextColor,
            ShowHeader = projectConfig.ShowHeader ?? @base.ShowHeader,
            ShowFooter = projectConfig.ShowFooter ?? @base.ShowFooter
        };
    }

    private static string ResolveWorkspacePath(string ownerPath, string rawPath)
    {
        var path = rawPath.Trim().Replace('\\', '/');
        if (path.StartsWith('/'))
            return FileSystemWorkspaceStore.NormalizePath(path);

        // Data source binding uses workspace-relative paths by default.
        // Only explicit "./" is treated as relative to the owning .buelo file.
        if (!path.StartsWith("./", StringComparison.Ordinal))
            return FileSystemWorkspaceStore.NormalizePath(path);

        path = path[2..];

        var ownerDir = ownerPath.Contains('/')
            ? ownerPath[..ownerPath.LastIndexOf('/')]
            : string.Empty;
        var combined = string.IsNullOrWhiteSpace(ownerDir) ? path : $"{ownerDir}/{path}";
        return FileSystemWorkspaceStore.NormalizePath(combined);
    }

    public static object ConvertToDynamic(object data)
    {
        if (data is JsonElement jsonElement)
            return JsonElementToExpando(jsonElement);

        return data;
    }

    public static object JsonElementToExpando(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var expando = new ExpandoObject() as IDictionary<string, object>;

                foreach (var prop in element.EnumerateObject())
                    expando[prop.Name] = JsonElementToExpando(prop.Value);

                return expando;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                              .Select(JsonElementToExpando)
                              .ToList();

            case JsonValueKind.String:
                if (element.TryGetDateTime(out var dt))
                    return dt;

                return element.GetString()!;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                    return l;

                return element.GetDecimal();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            default:
                return null!;
        }
    }
}
