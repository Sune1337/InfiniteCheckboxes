namespace RedisMessages;

using System.Text.Json;
using System.Threading.Channels;

using GrainInterfaces.War.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Messages;
using RedisMessages.Options;

using StackExchange.Redis;

public class RedisWarUpdatePublisherService : IHostedService, IRedisWarUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly CancellationTokenSource _publishWarUpdateMessagesTaskCancellationToken = new();
    private readonly string _redisConnectionString;
    private readonly Channel<WarUpdateMessage> _warUpdateChannel = Channel.CreateUnbounded<WarUpdateMessage>();

    private Task? _publishWarUpdateMessagesTask;

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
        await _warUpdateChannel.Writer.WriteAsync(
            new WarUpdateMessage
            {
                Id = id,
                War = war
            }
        );
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString, c => c.AbortOnConnectFail = false);
        _redisSubscriber = _redisConnection.GetSubscriber();
        _publishWarUpdateMessagesTask = PublishWarUpdateMessages();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null)
        {
            await _redisConnection.DisposeAsync();
        }

        if (_publishWarUpdateMessagesTask != null)
        {
            await _publishWarUpdateMessagesTaskCancellationToken.CancelAsync();
            await _publishWarUpdateMessagesTask;
        }
    }

    #endregion

    #region Methods

    private async Task PublishWarUpdateMessages()
    {
        try
        {
            while (_publishWarUpdateMessagesTaskCancellationToken.IsCancellationRequested == false)
            {
                var warUpdateMessage = await _warUpdateChannel.Reader.ReadAsync(_publishWarUpdateMessagesTaskCancellationToken.Token);
                if (_redisSubscriber == null)
                {
                    continue;
                }

                var serializedWarUpdate = JsonSerializer.Serialize(warUpdateMessage.War);
                await _redisSubscriber.PublishAsync(new RedisChannel($"WarUpdate:{warUpdateMessage.Id}", RedisChannel.PatternMode.Literal), serializedWarUpdate);
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

    #endregion
}
