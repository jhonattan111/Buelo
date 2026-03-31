using Microsoft.Extensions.DependencyInjection;

namespace Buelo.Engine;

public static class EngineExtensions
{
    public static IServiceCollection AddBueloEngine(this IServiceCollection services)
    {
        services.AddSingleton<TemplateEngine>();
        return services;
    }
}
