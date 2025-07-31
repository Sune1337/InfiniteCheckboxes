namespace RedisMessages.RedisPublisher;

using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.Options;

using StackExchange.Redis;

public abstract class RedisPublisherBase : IHostedService
{
    #region Fields

    private readonly ILogger _logger;
    private readonly string _redisConnectionString;
    private ConnectionMultiplexer? _redisConnection;
    private ISubscriber? _redisSubscriber;

    #endregion

    #region Constructors and Destructors

    public RedisPublisherBase(ILogger logger, IOptions<RedisPubSubOptions> options)
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

    protected async Task PublishJsonAsync<T>(string topic, T value)
    {
        if (_redisSubscriber == null)
        {
            return;
        }

        var serializedValue = JsonSerializer.Serialize(value);
        await _redisSubscriber.PublishAsync(new RedisChannel(topic, RedisChannel.PatternMode.Literal), serializedValue);
    }

    private void WhenConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "Redis connection failed for publisher {RedisPublisherName}.", GetType().Name);
    }

    private void WhenConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "Redis connection restored for publisher {RedisPublisherName}.", GetType().Name);
    }

    #endregion
}
