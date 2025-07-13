namespace RedisMessages;

using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Messages;
using RedisMessages.Options;

using StackExchange.Redis;

public class RedisCheckboxUpdatePublisherService : IHostedService, IRedisCheckboxUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly Channel<CheckboxUpdateMessage> _checkboxUpdateChannel = Channel.CreateUnbounded<CheckboxUpdateMessage>();
    private readonly CancellationTokenSource _publishCheckboxUpdateMessagesTaskCancellationToken = new();
    private readonly string _redisConnectionString;
    private Task? _publishCheckboxUpdateMessagesTask;

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
        await _checkboxUpdateChannel.Writer.WriteAsync(
            new CheckboxUpdateMessage
            {
                Id = id,
                CheckboxUpdate = new CheckboxUpdate
                {
                    Index = index,
                    Value = (byte)(value ? 1 : 0)
                }
            }
        );
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString, c => c.AbortOnConnectFail = false);
        _redisSubscriber = _redisConnection.GetSubscriber();
        _publishCheckboxUpdateMessagesTask = PublishCheckboxUpdateMessages();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null)
        {
            await _redisConnection.DisposeAsync();
        }

        if (_publishCheckboxUpdateMessagesTask != null)
        {
            await _publishCheckboxUpdateMessagesTaskCancellationToken.CancelAsync();
            await _publishCheckboxUpdateMessagesTask;
        }
    }

    #endregion

    #region Methods

    private async Task PublishCheckboxUpdateMessages()
    {
        try
        {
            while (_publishCheckboxUpdateMessagesTaskCancellationToken.IsCancellationRequested == false)
            {
                var checkboxUpdateMessage = await _checkboxUpdateChannel.Reader.ReadAsync(_publishCheckboxUpdateMessagesTaskCancellationToken.Token);
                if (_redisSubscriber == null)
                {
                    continue;
                }

                var serializedCheckboxUpdate = JsonSerializer.Serialize(checkboxUpdateMessage.CheckboxUpdate);
                await _redisSubscriber.PublishAsync(new RedisChannel($"CheckboxUpdate:{checkboxUpdateMessage.Id}", RedisChannel.PatternMode.Literal), serializedCheckboxUpdate);
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
