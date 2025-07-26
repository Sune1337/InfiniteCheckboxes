namespace MinesweeperHubv1;

using MinesweeperHubv1.Hubs;
using MinesweeperHubv1.MinesweeperObserver;

public static class MinesweeperHubv1Extensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddMinesweeperObserverService(this IServiceCollection services)
    {
        services.AddSingleton<MinesweeperObserverService>();
        services.AddSingleton<IMinesweeperObserverManager>(serviceProvider => serviceProvider.GetRequiredService<MinesweeperObserverService>());
        services.AddHostedService<MinesweeperObserverService>(serviceProvider => serviceProvider.GetRequiredService<MinesweeperObserverService>());

        return services;
    }

    public static void MapMinesweeperHubv1(this IEndpointRouteBuilder endpoints, string path)
    {
        endpoints.MapHub<MinesweeperHub>(path);
    }

    #endregion
}
