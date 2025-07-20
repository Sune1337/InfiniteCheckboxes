namespace RedisMessages.UserUpdate;

using System.Text.Json;

using GrainInterfaces.User.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Options;

using StackExchange.Redis;

public class RedisUserUpdatePublisherService : IHostedService, IRedisUserUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly string _redisConnectionString;

    #endregion

    #region Constructors and Destructors

    public RedisUserUpdatePublisherService(IOptions<RedisMessagePublisherOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishUserUpdateAsync(string id, User user)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        var serializedUserUpdate = JsonSerializer.Serialize(user);
        await _redisSubscriber.PublishAsync(new RedisChannel($"UserUpdate:{id}", RedisChannel.PatternMode.Literal), serializedUserUpdate);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString, c => c.AbortOnConnectFail = false);
        _redisSubscriber = _redisConnection.GetSubscriber();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null)
        {
            await _redisConnection.DisposeAsync();
        }
    }

    #endregion
}
