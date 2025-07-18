namespace RedisMessages;

using System.Text.Json;

using GrainInterfaces.War.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Options;

using StackExchange.Redis;

public class RedisWarUpdatePublisherService : IHostedService, IRedisWarUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly string _redisConnectionString;

    #endregion

    #region Constructors and Destructors

    public RedisWarUpdatePublisherService(IOptions<RedisMessagePublisherOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishWarUpdateAsync(long id, War war)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        var serializedWarUpdate = JsonSerializer.Serialize(war);
        await _redisSubscriber.PublishAsync(new RedisChannel($"WarUpdate:{id}", RedisChannel.PatternMode.Literal), serializedWarUpdate);
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
