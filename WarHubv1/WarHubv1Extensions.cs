namespace WarHubv1;

using WarHubv1.Hubs;
using WarHubv1.WarObserver;

public static class WarHubv1Extensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddWarObserverService(this IServiceCollection services)
    {
        services.AddSingleton<WarObserverService>();
        services.AddSingleton<IWarObserverManager>(serviceProvider => serviceProvider.GetRequiredService<WarObserverService>());
        services.AddHostedService<WarObserverService>(serviceProvider => serviceProvider.GetRequiredService<WarObserverService>());

        return services;
    }

    public static void MapWarHubv1(this IEndpointRouteBuilder endpoints, string path)
    {
        endpoints.MapHub<WarHub>(path);
    }

    #endregion
}
