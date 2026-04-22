using Buelo.Contracts;
using Buelo.Engine.Renderers;
using Buelo.Engine.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Buelo.Engine;

public static class EngineExtensions
{
    /// <summary>
    /// Registers Buelo engine services.
    /// <para>
    /// Registered services:
    /// <list type="bullet">
    ///   <item><description><see cref="TemplateEngine"/> – singleton report renderer.</description></item>
    ///   <item><description><see cref="ITemplateStore"/> → <see cref="InMemoryTemplateStore"/> – singleton template store (swap for a DB or file-system implementation for persistence).</description></item>
    ///   <item><description><see cref="IHelperRegistry"/> → <see cref="DefaultHelperRegistry"/> – singleton formatting helper (override with your own implementation by registering before calling this method).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddBueloEngine(this IServiceCollection services)
    {
        // Allow callers to register their own IHelperRegistry before calling AddBueloEngine().
        services.TryAddSingleton<IHelperRegistry, DefaultHelperRegistry>();

        // TryAdd so that AddBueloFileSystemStore() (or any custom store) registered first takes precedence.
        services.TryAddSingleton<ITemplateStore, InMemoryTemplateStore>();
        services.TryAddSingleton<IGlobalArtefactStore, InMemoryGlobalArtefactStore>();
        services.TryAddSingleton<IWorkspaceStore>(sp =>
            new FileSystemWorkspaceStore(ResolveStorePath(sp, rootPath: null)));
        services.TryAddSingleton<IWorkspaceFileEnumerator>(sp =>
            new FileSystemWorkspaceFileEnumerator(ResolveStorePath(sp, rootPath: null)));
        services.AddSingleton<IFileValidator, JsonFileValidator>();
        services.AddSingleton<IFileValidator, CsharpFileValidator>();
        services.AddSingleton<FileValidatorRegistry>();
        services.AddSingleton<TemplateEngine>();
        services.AddSingleton<IOutputRenderer, PdfRenderer>();
        services.AddSingleton<IOutputRenderer, ExcelRenderer>();
        services.AddSingleton<OutputRendererRegistry>();
        return services;
    }

    /// <summary>
    /// Registers a <see cref="FileSystemTemplateStore"/> as the <see cref="ITemplateStore"/>
    /// and then calls <see cref="AddBueloEngine"/> to ensure all engine services are registered.
    /// <para>
    /// Configure the storage root path via <c>appsettings.json</c>:
    /// <code>{ "Buelo": { "TemplateStorePath": "/data/templates" } }</code>
    /// Falls back to a <c>templates</c> sub-directory relative to the current working directory.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBueloFileSystemStore(this IServiceCollection services, string? rootPath = null)
    {
        services.TryAddSingleton<ITemplateStore>(sp =>
        {
            string path = ResolveStorePath(sp, rootPath);
            return new FileSystemTemplateStore(path);
        });

        services.TryAddSingleton<IGlobalArtefactStore>(sp =>
        {
            string path = ResolveStorePath(sp, rootPath);
            return new FileSystemGlobalArtefactStore(path);
        });

        services.TryAddSingleton<IWorkspaceStore>(sp =>
        {
            string path = ResolveStorePath(sp, rootPath);
            return new FileSystemWorkspaceStore(path);
        });

        services.TryAddSingleton<IWorkspaceFileEnumerator>(sp =>
            new FileSystemWorkspaceFileEnumerator(ResolveStorePath(sp, rootPath)));

        return services.AddBueloEngine();
    }

    private static string ResolveStorePath(IServiceProvider sp, string? rootPath)
        => rootPath
            ?? sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>()?["Buelo:TemplateStorePath"]
            ?? "templates";
}

