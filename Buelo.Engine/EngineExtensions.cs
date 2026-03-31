using Buelo.Contracts;
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
    ///   <item><description><see cref="ITemplateStore"/> → <see cref="InMemoryTemplateStore"/> – singleton template store (swap for a DB implementation for persistence).</description></item>
    ///   <item><description><see cref="IHelperRegistry"/> → <see cref="DefaultHelperRegistry"/> – singleton formatting helper (override with your own implementation by registering before calling this method).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddBueloEngine(this IServiceCollection services)
    {
        // Allow callers to register their own IHelperRegistry before calling AddBueloEngine().
        // TryAddSingleton only registers if no other registration exists yet.
        services.TryAddSingleton<IHelperRegistry, DefaultHelperRegistry>();

        services.AddSingleton<ITemplateStore, InMemoryTemplateStore>();
        services.AddSingleton<TemplateEngine>();
        return services;
    }
}

