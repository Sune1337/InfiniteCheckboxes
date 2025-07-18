namespace CheckboxHubv1.CheckboxObserver;

using System.Text.Json;
using System.Threading.Channels;

using CheckboxHubv1.Hubs;
using CheckboxHubv1.Options;
using CheckboxHubv1.Statistics;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using RedisMessages;
using RedisMessages.Messages;

using StackExchange.Redis;

using Two56bitId;

using ValueDebouncer;

public class CheckboxPageUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<int, byte> DebounceValues = new(logger);
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
    private readonly Channel<CheckboxUpdateMessage> _checkboxUpdateChannel = Channel.CreateUnbounded<CheckboxUpdateMessage>();
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _readCheckboxUpdateMessagesTaskCancellationToken = new();
    private readonly string _redisConnectionString;
    private readonly IStatisticsObserverManager _statisticsObserverManager;
    private readonly Dictionary<string, CheckboxPageUpdates> _subscriptions = new();
    private readonly Lock _subscriptionsLock = new();
    private Task? _readCheckboxUpdateMessagesTask;

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
        _readCheckboxUpdateMessagesTask = ReadCheckboxUpdateMessages();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null)
        {
            _redisConnection.ConnectionRestored -= WhenConnectionRestored;
            await _redisConnection.DisposeAsync();
        }

        if (_readCheckboxUpdateMessagesTask != null)
        {
            await _readCheckboxUpdateMessagesTaskCancellationToken.CancelAsync();
            await _readCheckboxUpdateMessagesTask;
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
                checkboxPageUpdates.DebounceValues.EmitValues += async values =>
                {
                    var base64Id = Convert.ToBase64String(id.HexStringToByteArray());
                    await _checkboxHubContext.Clients
                        .Group($"{HubGroups.CheckboxGroupPrefix}_{id}")
                        .SendAsync("CheckboxesUpdate", base64Id, values.Select(v => (int[]) [v.Key, v.Value]));
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
        }

        await _statisticsObserverManager.AddCheckboxSubscribers(id, -1);
    }

    #endregion

    #region Methods

    private async Task ReadCheckboxUpdateMessages()
    {
        try
        {
            while (_readCheckboxUpdateMessagesTaskCancellationToken.IsCancellationRequested == false)
            {
                var checkboxUpdateMessage = await _checkboxUpdateChannel.Reader.ReadAsync(_readCheckboxUpdateMessagesTaskCancellationToken.Token);

                DebounceValues<int, byte>? debounceValues;
                lock (_subscriptionsLock)
                {
                    debounceValues = _subscriptions.TryGetValue(checkboxUpdateMessage.Id, out var checkboxPageUpdates) ? checkboxPageUpdates.DebounceValues : null;
                }

                debounceValues?.DebounceValue(checkboxUpdateMessage.CheckboxUpdate.Index, checkboxUpdateMessage.CheckboxUpdate.Value);
            }
        }

        catch (OperationCanceledException)
        {
            // Stop reading the channel.
        }

        catch (ChannelClosedException)
        {
            // Stop reading the channel.
        }
    }

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
            case "CheckboxUpdate":
                var checkboxUpdate = JsonSerializer.Deserialize<CheckboxUpdate>(redisValueAsString);
                if (checkboxUpdate == null)
                {
                    return;
                }

                _checkboxUpdateChannel.Writer.TryWrite(
                    new CheckboxUpdateMessage
                    {
                        Id = key[1],
                        CheckboxUpdate = checkboxUpdate
                    }
                );
                break;
        }
    }

    #endregion
}
