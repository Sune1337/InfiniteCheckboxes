namespace CheckboxHubv1;

using CheckboxHubv1.CheckboxObservers;
using CheckboxHubv1.Hubs;

public static class CheckboxHubv1Extensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddCheckboxObserverService(this IServiceCollection services)
    {
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
