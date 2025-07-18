namespace CheckboxHubv1;

using CheckboxHubv1.CheckboxObserver;
using CheckboxHubv1.Hubs;
using CheckboxHubv1.Statistics;

public static class CheckboxHubv1Extensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddCheckboxServices(this IServiceCollection services)
    {
        services.AddSingleton<StatisticsObserverObserverService>();
        services.AddSingleton<IStatisticsObserverManager>(serviceProvider => serviceProvider.GetRequiredService<StatisticsObserverObserverService>());
        services.AddHostedService<StatisticsObserverObserverService>(serviceProvider => serviceProvider.GetRequiredService<StatisticsObserverObserverService>());

        services.AddSingleton<CheckboxObserverService>();
        services.AddSingleton<ICheckboxObserverManager>(serviceProvider => serviceProvider.GetRequiredService<CheckboxObserverService>());
        services.AddHostedService<CheckboxObserverService>(serviceProvider => serviceProvider.GetRequiredService<CheckboxObserverService>());

        return services;
    }

    public static void MapCheckboxHubv1(this IEndpointRouteBuilder endpoints, string path)
    {
        endpoints.MapHub<CheckboxHub>(path);
    }

    #endregion
}
