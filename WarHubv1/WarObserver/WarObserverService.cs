namespace WarHubv1.WarObserver;

using System.Text.Json;

using GrainInterfaces.War.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using ValueDebouncer;

using WarHubv1.Hubs;
using WarHubv1.Options;

public class WarUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<long, War> DebounceValues = new(logger);
    public int Count = 1;

    #endregion
}

public class WarObserverService : IHostedService, IWarObserverManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly ILogger _logger;
    private readonly string _redisConnectionString;
    private readonly Dictionary<long, WarUpdates> _subscriptions = new();
    private readonly Lock _subscriptionsLock = new();
    private readonly IHubContext<WarHub> _warHubContext;

    #endregion

    #region Constructors and Destructors

    public WarObserverService(IOptions<WarObserverOptions> options, IHubContext<WarHub> warHubContext, ILogger<WarObserverService> logger)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
        _warHubContext = warHubContext;
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

    public async Task SubscribeAsync(long id)
    {
        var startSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(id, out var subscription) == false)
            {
                var warUpdates = new WarUpdates(_logger);
                warUpdates.DebounceValues.EmitValues += async values =>
                {
                    foreach (var value in values.Values)
                    {
                        await _warHubContext.Clients
                            .Group($"{HubGroups.WarGroupPrefix}_{id}")
                            .SendAsync("WarsUpdate", id, value);
                    }
                };
                _subscriptions.Add(id, warUpdates);
                startSubscribe = true;
            }
            else
            {
                subscription.Count++;
            }
        }

        if (startSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.SubscribeAsync(new RedisChannel($"WarUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
        }
    }

    public async Task UnsubscribeAsync(long id)
    {
        var stopSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(id, out var warUpdates) == false)
            {
                return;
            }

            warUpdates.Count--;
            if (warUpdates.Count == 0)
            {
                stopSubscribe = true;
                _subscriptions.Remove(id);
            }
        }

        if (stopSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel($"WarUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
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
                _redisSubscriber.Subscribe(new RedisChannel($"WarUpdate:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
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

        if (long.TryParse(key[1], out var id) == false)
        {
            return;
        }

        switch (key[0])
        {
            case "WarUpdate":
                var warUpdate = JsonSerializer.Deserialize<War>(redisValueAsString);
                if (warUpdate == null)
                {
                    return;
                }

                DebounceValues<long, War>? debounceValues;
                lock (_subscriptionsLock)
                {
                    debounceValues = _subscriptions.TryGetValue(id, out var warUpdates) ? warUpdates.DebounceValues : null;
                }

                debounceValues?.DebounceValue(id, warUpdate);
                break;
        }
    }

    #endregion
}
