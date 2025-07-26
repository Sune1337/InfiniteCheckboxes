namespace MinesweeperHubv1.MinesweeperObserver;

using System.Text.Json;

using GrainInterfaces.Minesweeper.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using MinesweeperHubv1.Hubs;
using MinesweeperHubv1.Options;

using StackExchange.Redis;

using Two56bitId;

using ValueDebouncer;

public class MinesweeperUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<string, Dictionary<int, int>> DebounceCounts = new(logger, 0);

    public readonly DebounceValues<string, Minesweeper> DebounceMinesweeper = new(logger);
    public int Count = 1;

    #endregion
}

public class MinesweeperObserverService : IHostedService, IMinesweeperObserverManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly ILogger _logger;
    private readonly IHubContext<MinesweeperHub> _minesweeperHubContext;
    private readonly string _redisConnectionString;
    private readonly Dictionary<string, MinesweeperUpdates> _subscriptions = new();
    private readonly Lock _subscriptionsLock = new();

    #endregion

    #region Constructors and Destructors

    public MinesweeperObserverService(IOptions<MinesweeperObserverOptions> options, IHubContext<MinesweeperHub> minesweeperHubContext, ILogger<MinesweeperObserverService> logger)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
        _minesweeperHubContext = minesweeperHubContext;
        _logger = logger;
    }

    #endregion

    #region Public Methods and Operators

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString, c => c.AbortOnConnectFail = false);
        _redisSubscriber = _redisConnection.GetSubscriber();
        _redisConnection.ConnectionRestored += WhenConnectionRestored;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null)
        {
            _redisConnection.ConnectionRestored -= WhenConnectionRestored;
            await _redisConnection.DisposeAsync();
        }
    }

    public async Task SubscribeAsync(string id)
    {
        var startSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(id, out var subscription) == false)
            {
                var minesweeperUpdates = new MinesweeperUpdates(_logger);

                minesweeperUpdates.DebounceMinesweeper.EmitValues += async values =>
                {
                    foreach (var value in values.Values)
                    {
                        await _minesweeperHubContext.Clients
                            .Group($"{HubGroups.MinesweeperGroupPrefix}_{id}")
                            .SendAsync("MinesweeperUpdate", id.HexStringToByteArray(), value);
                    }
                };

                minesweeperUpdates.DebounceCounts.EmitValues += async values =>
                {
                    foreach (var value in values.Values)
                    {
                        await _minesweeperHubContext.Clients
                            .Group($"{HubGroups.MinesweeperGroupPrefix}_{id}")
                            .SendAsync("MinesweeperCounts", id.HexStringToByteArray(), value);
                    }
                };

                _subscriptions.Add(id, minesweeperUpdates);
                startSubscribe = true;
            }
            else
            {
                subscription.Count++;
            }
        }

        if (startSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.SubscribeAsync(new RedisChannel($"MinesweeperUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
            await _redisSubscriber.SubscribeAsync(new RedisChannel($"MinesweeperCounts:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
        }
    }

    public async Task UnsubscribeAsync(string id)
    {
        var stopSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(id, out var minesweeperUpdates) == false)
            {
                return;
            }

            minesweeperUpdates.Count--;
            if (minesweeperUpdates.Count == 0)
            {
                stopSubscribe = true;
                _subscriptions.Remove(id);
            }
        }

        if (stopSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel($"MinesweeperUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel($"MinesweeperCounts:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
        }
    }

    #endregion

    #region Methods

    private void WhenConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        lock (_subscriptionsLock)
        {
            foreach (var key in _subscriptions.Keys)
            {
                _redisSubscriber.Subscribe(new RedisChannel($"MinesweeperUpdate:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
                _redisSubscriber.Subscribe(new RedisChannel($"MinesweeperCounts:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
            }
        }
    }

    private void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue)
    {
        if (!redisValue.HasValue)
        {
            return;
        }

        var redisValueAsString = redisValue.ToString();

        var key = redisChannel.ToString().Split(':');
        if (key.Length != 2)
        {
            return;
        }

        switch (key[0])
        {
            case "MinesweeperUpdate":
                var minesweeperUpdate = JsonSerializer.Deserialize<Minesweeper>(redisValueAsString);
                if (minesweeperUpdate == null)
                {
                    return;
                }

                DebounceValues<string, Minesweeper>? debouncedMinesweeper;
                lock (_subscriptionsLock)
                {
                    debouncedMinesweeper = _subscriptions.TryGetValue(key[1], out var minesweeperUpdates) ? minesweeperUpdates.DebounceMinesweeper : null;
                }

                debouncedMinesweeper?.DebounceValue(key[1], minesweeperUpdate);
                break;

            case "MinesweeperCounts":
                var minesweeperCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(redisValueAsString);
                if (minesweeperCounts == null)
                {
                    return;
                }

                DebounceValues<string, Dictionary<int, int>>? debouncedCounts;
                lock (_subscriptionsLock)
                {
                    debouncedCounts = _subscriptions.TryGetValue(key[1], out var minesweeperUpdates) ? minesweeperUpdates.DebounceCounts : null;
                }

                debouncedCounts?.DebounceValue(key[1], minesweeperCounts);
                break;
        }
    }

    #endregion
}
