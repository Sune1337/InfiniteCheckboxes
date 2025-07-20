namespace CheckboxHubv1;

using CheckboxHubv1.CheckboxObserver;
using CheckboxHubv1.Hubs;
using CheckboxHubv1.Statistics;
using CheckboxHubv1.UserObserver;

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

        services.AddSingleton<UserObserverService>();
        services.AddSingleton<IUserObserverManager>(serviceProvider => serviceProvider.GetRequiredService<UserObserverService>());
        services.AddHostedService<UserObserverService>(serviceProvider => serviceProvider.GetRequiredService<UserObserverService>());

        return services;
    }

    public static void MapCheckboxHubv1(this IEndpointRouteBuilder endpoints, string path)
    {
        endpoints.MapHub<CheckboxHub>(path);
    }

    #endregion
}
