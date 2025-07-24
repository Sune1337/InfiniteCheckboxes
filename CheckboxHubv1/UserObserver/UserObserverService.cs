namespace CheckboxHubv1.UserObserver;

using System.Text.Json;

using CheckboxHubv1.Hubs;
using CheckboxHubv1.Options;

using GrainInterfaces.User.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using ValueDebouncer;

public class UserUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<string, User> DebounceValues = new(logger);
    public int Count = 1;

    #endregion
}

public class UserObserverService : IHostedService, IUserObserverManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly IHubContext<CheckboxHub> _checkboxHubContext;
    private readonly ILogger _logger;
    private readonly string _redisConnectionString;
    private readonly Dictionary<string, UserUpdates> _subscriptions = new();
    private readonly Lock _subscriptionsLock = new();

    #endregion

    #region Constructors and Destructors

    public UserObserverService(ILogger<UserObserverService> logger, IOptions<UserObserverOptions> options, IHubContext<CheckboxHub> checkboxHubContext)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _logger = logger;
        _redisConnectionString = options.Value.RedisConnectionString;
        _checkboxHubContext = checkboxHubContext;
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
                var userUpdates = new UserUpdates(_logger);
                userUpdates.DebounceValues.EmitValues += async values =>
                {
                    foreach (var value in values.Values)
                    {
                        await _checkboxHubContext.Clients
                            .User(id)
                            .SendAsync("UB", new { value.GoldBalance });
                    }
                };
                _subscriptions.Add(id, userUpdates);
                startSubscribe = true;
            }
            else
            {
                subscription.Count++;
            }
        }

        if (startSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.SubscribeAsync(new RedisChannel($"UserUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
        }
    }

    public async Task UnsubscribeAsync(string id)
    {
        var stopSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(id, out var userUpdates) == false)
            {
                return;
            }

            userUpdates.Count--;
            if (userUpdates.Count == 0)
            {
                stopSubscribe = true;
                _subscriptions.Remove(id);
            }
        }

        if (stopSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel($"UserUpdate:{id}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
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
                _redisSubscriber.Subscribe(new RedisChannel($"UserUpdate:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceived);
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
            case "UserUpdate":
                var user = JsonSerializer.Deserialize<User>(redisValueAsString);
                if (user == null)
                {
                    return;
                }

                DebounceValues<string, User>? debounceValues;
                lock (_subscriptionsLock)
                {
                    debounceValues = _subscriptions.TryGetValue(key[1], out var userUpdates) ? userUpdates.DebounceValues : null;
                }

                debounceValues?.DebounceValue(key[0], user);
                break;
        }
    }

    #endregion
}
