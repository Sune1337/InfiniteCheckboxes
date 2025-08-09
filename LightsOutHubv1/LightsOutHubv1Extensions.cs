namespace LightsOutHubv1;

using LightsOutHubv1.Hubs;
using LightsOutHubv1.LightsOutObserver;

public static class LightsOutHubv1Extensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddLightsOutObserverService(this IServiceCollection services)
    {
        services.AddSingleton<LightsOutObserverService>();
        services.AddSingleton<ILightsOutObserverManager>(serviceProvider => serviceProvider.GetRequiredService<LightsOutObserverService>());
        services.AddHostedService<LightsOutObserverService>(serviceProvider => serviceProvider.GetRequiredService<LightsOutObserverService>());

        return services;
    }

    public static void MapLightsOutHubv1(this IEndpointRouteBuilder endpoints, string path)
    {
        endpoints.MapHub<LightsOutHub>(path);
    }

    #endregion
}
