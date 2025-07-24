namespace CheckboxHubv1.CheckboxObserver;

using System.Text.Json;

using CheckboxHubv1.Hubs;
using CheckboxHubv1.Options;
using CheckboxHubv1.Statistics;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using RedisMessages.CheckboxUpdate.Models;

using StackExchange.Redis;

using Two56bitId;

using ValueDebouncer;

public class CheckboxPageUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<int, byte> DebounceCheckboxUpdate = new(logger);
    public readonly DebounceValues<int, bool> DebounceGoldSpot = new(logger);
    public int Count = 1;

    #endregion
}

public class CheckboxObserverService : IHostedService, ICheckboxObserverManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly IHubContext<CheckboxHub> _checkboxHubContext;
    private readonly ILogger _logger;
    private readonly string _redisConnectionString;
    private readonly IStatisticsObserverManager _statisticsObserverManager;
    private readonly Dictionary<string, CheckboxPageUpdates> _subscriptions = new();
    private readonly Lock _subscriptionsLock = new();

    #endregion

    #region Constructors and Destructors

    public CheckboxObserverService(ILogger<CheckboxObserverService> logger, IOptions<CheckboxObserverOptions> options, IHubContext<CheckboxHub> checkboxHubContext, IStatisticsObserverManager statisticsObserverManager)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _logger = logger;
        _redisConnectionString = options.Value.RedisConnectionString;
        _checkboxHubContext = checkboxHubContext;
        _statisticsObserverManager = statisticsObserverManager;
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
                var checkboxPageUpdates = new CheckboxPageUpdates(_logger);
                var byteId = id.HexStringToByteArray();

                checkboxPageUpdates.DebounceCheckboxUpdate.EmitValues += async values =>
                {
                    await _checkboxHubContext.Clients
                        .Group($"{HubGroups.CheckboxGroupPrefix}_{id}")
                        .SendAsync("CheckboxesUpdate", byteId, BitCoding.IndexAndBoolCoder.Encode(values));
                };

                checkboxPageUpdates.DebounceGoldSpot.EmitValues += async values =>
                {
                    await _checkboxHubContext.Clients
                        .Group($"{HubGroups.CheckboxGroupPrefix}_{id}")
                        .SendAsync("GoldSpot", byteId, values.Keys);
                };

                _subscriptions.Add(id, checkboxPageUpdates);
                startSubscribe = true;
            }
            else
            {
                subscription.Count++;
            }
        }

        if (startSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.SubscribeAsync(new RedisChannel($"CheckboxUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
            await _redisSubscriber.SubscribeAsync(new RedisChannel($"GoldSpot:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
        }

        await _statisticsObserverManager.AddCheckboxSubscribers(id, 1);
    }

    public async Task UnsubscribeAsync(string id)
    {
        var stopSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(id, out var checkboxPageUpdates) == false)
            {
                return;
            }

            checkboxPageUpdates.Count--;
            if (checkboxPageUpdates.Count == 0)
            {
                stopSubscribe = true;
                _subscriptions.Remove(id);
            }
        }

        if (stopSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel($"CheckboxUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel($"GoldSpot:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
        }

        await _statisticsObserverManager.AddCheckboxSubscribers(id, -1);
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
                _redisSubscriber.Subscribe(new RedisChannel($"CheckboxUpdate:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
                _redisSubscriber.Subscribe(new RedisChannel($"GoldSpot:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
            }
        }
    }

    private void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue)
    {
        if (!redisValue.HasValue)
        {
            return;
        }

        var key = redisChannel.ToString().Split(':');
        if (key.Length != 2)
        {
            return;
        }

        switch (key[0])
        {
            case "CheckboxUpdate":
                var redisValueAsString = redisValue.ToString();
                var checkboxUpdate = JsonSerializer.Deserialize<CheckboxUpdate>(redisValueAsString);
                if (checkboxUpdate == null)
                {
                    return;
                }

                DebounceValues<int, byte>? debounceCheckboxUpdate;
                lock (_subscriptionsLock)
                {
                    debounceCheckboxUpdate = _subscriptions.TryGetValue(key[1], out var checkboxPageUpdates) ? checkboxPageUpdates.DebounceCheckboxUpdate : null;
                }

                debounceCheckboxUpdate?.DebounceValue(checkboxUpdate.Index, checkboxUpdate.Value);
                break;

            case "GoldSpot":
                if (redisValue.TryParse(out int redisValueAsInt) == false)
                {
                    break;
                }

                DebounceValues<int, bool>? debounceGoldSpot;
                lock (_subscriptionsLock)
                {
                    debounceGoldSpot = _subscriptions.TryGetValue(key[1], out var checkboxPageUpdates) ? checkboxPageUpdates.DebounceGoldSpot : null;
                }

                debounceGoldSpot?.DebounceValue(redisValueAsInt, true);

                break;
        }
    }

    #endregion
}
