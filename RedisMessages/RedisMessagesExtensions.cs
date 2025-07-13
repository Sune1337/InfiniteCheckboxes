namespace RedisMessages;

using Microsoft.Extensions.DependencyInjection;

public static class RedisMessagesExtensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddRedisMessagePublishers(this IServiceCollection services)
    {
        // CheckboxUpdate.
        services.AddSingleton<RedisCheckboxUpdatePublisherService>();
        services.AddSingleton<IRedisCheckboxUpdatePublisherManager>(serviceProvider => serviceProvider.GetRequiredService<RedisCheckboxUpdatePublisherService>());
        services.AddHostedService<RedisCheckboxUpdatePublisherService>(serviceProvider => serviceProvider.GetRequiredService<RedisCheckboxUpdatePublisherService>());

        // WarUpdate.
        services.AddSingleton<RedisWarUpdatePublisherService>();
        services.AddSingleton<IRedisWarUpdatePublisherManager>(serviceProvider => serviceProvider.GetRequiredService<RedisWarUpdatePublisherService>());
        services.AddHostedService<RedisWarUpdatePublisherService>(serviceProvider => serviceProvider.GetRequiredService<RedisWarUpdatePublisherService>());

        return services;
    }

    #endregion
}
