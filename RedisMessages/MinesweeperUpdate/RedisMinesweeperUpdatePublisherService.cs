namespace RedisMessages.MinesweeperUpdate;

using System.Text.Json;

using GrainInterfaces.Minesweeper.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Options;

using StackExchange.Redis;

public class RedisMinesweeperUpdatePublisherService : IHostedService, IRedisMinesweeperUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly string _redisConnectionString;

    #endregion

    #region Constructors and Destructors

    public RedisMinesweeperUpdatePublisherService(IOptions<RedisMessagePublisherOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishCountsAsync(string id, Dictionary<int, int> counts)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        var serializedCounts = JsonSerializer.Serialize(counts);
        await _redisSubscriber.PublishAsync(new RedisChannel($"MinesweeperCounts:{id}", RedisChannel.PatternMode.Literal), serializedCounts);
    }

    public async Task PublishMinesweeperAsync(string id, Minesweeper minesweeper)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        var serializedMinesweeper = JsonSerializer.Serialize(minesweeper);
        await _redisSubscriber.PublishAsync(new RedisChannel($"MinesweeperUpdate:{id}", RedisChannel.PatternMode.Literal), serializedMinesweeper);
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
