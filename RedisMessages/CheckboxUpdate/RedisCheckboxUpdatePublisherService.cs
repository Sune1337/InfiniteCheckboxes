namespace RedisMessages.CheckboxUpdate;

using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Options;

using StackExchange.Redis;

public class RedisCheckboxUpdatePublisherService : IHostedService, IRedisCheckboxUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly string _redisConnectionString;

    #endregion

    #region Constructors and Destructors

    public RedisCheckboxUpdatePublisherService(IOptions<RedisMessagePublisherOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishCheckboxUpdateAsync(string id, int index, bool value)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        var serializedCheckboxUpdate = JsonSerializer.Serialize(new CheckboxUpdate.Models.CheckboxUpdate
        {
            Index = index,
            Value = (byte)(value ? 1 : 0)
        });

        await _redisSubscriber.PublishAsync(new RedisChannel($"CheckboxUpdate:{id}", RedisChannel.PatternMode.Literal), serializedCheckboxUpdate);
    }

    public async Task PublishGoldSpotAsync(string id, int index)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        await _redisSubscriber.PublishAsync(new RedisChannel($"GoldSpot:{id}", RedisChannel.PatternMode.Literal), index);
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
