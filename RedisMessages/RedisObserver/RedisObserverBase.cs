namespace RedisMessages.RedisObserver;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.Options;

using StackExchange.Redis;

public class SubscriptionState<T>
{
    #region Public Properties

    public int NumberOfSubscribers { get; set; }
    public T? State { get; set; }

    #endregion
}

public abstract class RedisObserverBase<T> : IHostedService
{
    #region Fields

    private readonly ILogger _logger;
    private readonly string _redisConnectionString;
    private readonly Dictionary<string, SubscriptionState<T>> _subscriptions = new();
    private readonly Lock _subscriptionsLock = new();
    private ConnectionMultiplexer? _redisConnection;
    private ISubscriber? _redisSubscriber;

    #endregion

    #region Constructors and Destructors

    public RedisObserverBase(ILogger logger, IOptions<RedisPubSubOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _logger = logger;
        _redisConnectionString = options.Value.RedisConnectionString;
    }

    #endregion

    #region Public Methods and Operators

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString, c => c.AbortOnConnectFail = false);
        _redisSubscriber = _redisConnection.GetSubscriber();
        _redisConnection.ConnectionFailed += WhenConnectionFailed;
        _redisConnection.ConnectionRestored += WhenConnectionRestored;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null)
        {
            _redisConnection.ConnectionFailed += WhenConnectionFailed;
            _redisConnection.ConnectionRestored += WhenConnectionRestored;
            await _redisConnection.DisposeAsync();
        }
    }

    #endregion

    #region Methods

    protected virtual T? CreateState(string topic)
    {
        return default;
    }

    protected virtual void DestroyState(string topic, T? state)
    {
    }

    protected async Task SubscribeTopicAsync(string topic)
    {
        var startSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(topic, out var subscriptionState) == false)
            {
                _subscriptions.Add(topic, new SubscriptionState<T> { NumberOfSubscribers = 1, State = CreateState(topic) });
                startSubscribe = true;
            }
            else
            {
                subscriptionState.NumberOfSubscribers++;
            }
        }

        if (startSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.SubscribeAsync(new RedisChannel(topic, RedisChannel.PatternMode.Literal), WhenRedisMessageReceivedInternal);
        }
    }

    protected async Task UnsubscribeTopicAsync(string topic)
    {
        var stopSubscribe = false;
        lock (_subscriptionsLock)
        {
            if (_subscriptions.TryGetValue(topic, out var subscriptionState) == false)
            {
                return;
            }

            subscriptionState.NumberOfSubscribers--;
            if (subscriptionState.NumberOfSubscribers == 0)
            {
                DestroyState(topic, subscriptionState.State);
                stopSubscribe = true;
                _subscriptions.Remove(topic);
            }
        }

        if (stopSubscribe && _redisSubscriber != null)
        {
            await _redisSubscriber.UnsubscribeAsync(new RedisChannel(topic, RedisChannel.PatternMode.Literal), WhenRedisMessageReceivedInternal);
        }
    }

    protected abstract void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue, T? state);

    private void WhenConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "Redis connection failed for observer {RedisObserverName}.", GetType().Name);
    }

    private void WhenConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "Redis connection restored for observer {RedisObserverName}.", GetType().Name);

        if (_redisSubscriber == null)
        {
            return;
        }

        lock (_subscriptionsLock)
        {
            foreach (var key in _subscriptions.Keys)
            {
                _redisSubscriber.Subscribe(new RedisChannel($"UserUpdate:{key}", RedisChannel.PatternMode.Literal), WhenRedisMessageReceivedInternal);
            }
        }
    }

    private void WhenRedisMessageReceivedInternal(RedisChannel redisChannel, RedisValue redisValue)
    {
        string? topic = redisChannel;
        if (topic == null)
        {
            return;
        }

        SubscriptionState<T>? subscriptionState;
        lock (_subscriptionsLock)
        {
            _subscriptions.TryGetValue(topic, out subscriptionState);
        }

        if (subscriptionState == null)
        {
            return;
        }

        WhenRedisMessageReceived(redisChannel, redisValue, subscriptionState.State);
    }

    #endregion
}
