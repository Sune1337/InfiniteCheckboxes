namespace RedisMessages;

using Microsoft.Extensions.DependencyInjection;

using RedisMessages.CheckboxUpdate;
using RedisMessages.MinesweeperUpdate;
using RedisMessages.UserUpdate;
using RedisMessages.WarUpdate;

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

        // UserUpdate.
        services.AddSingleton<RedisUserUpdatePublisherService>();
        services.AddSingleton<IRedisUserUpdatePublisherManager>(serviceProvider => serviceProvider.GetRequiredService<RedisUserUpdatePublisherService>());
        services.AddHostedService<RedisUserUpdatePublisherService>(serviceProvider => serviceProvider.GetRequiredService<RedisUserUpdatePublisherService>());

        // MinesweeperUpdate.
        services.AddSingleton<RedisMinesweeperUpdatePublisherService>();
        services.AddSingleton<IRedisMinesweeperUpdatePublisherManager>(serviceProvider => serviceProvider.GetRequiredService<RedisMinesweeperUpdatePublisherService>());
        services.AddHostedService<RedisMinesweeperUpdatePublisherService>(serviceProvider => serviceProvider.GetRequiredService<RedisMinesweeperUpdatePublisherService>());

        return services;
    }

    #endregion
}
