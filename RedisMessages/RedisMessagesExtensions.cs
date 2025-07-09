namespace RedisMessages;

using Microsoft.Extensions.DependencyInjection;

public static class RedisMessagesExtensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddRedisMessagePublisher(this IServiceCollection services)
    {
        services.AddSingleton<RedisMessagePublisherService>();
        services.AddSingleton<IRedisMessagePublisherManager>(serviceProvider => serviceProvider.GetRequiredService<RedisMessagePublisherService>());
        services.AddHostedService<RedisMessagePublisherService>(serviceProvider => serviceProvider.GetRequiredService<RedisMessagePublisherService>());
        return services;
    }

    #endregion
}
