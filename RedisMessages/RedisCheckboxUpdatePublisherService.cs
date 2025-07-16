namespace RedisMessages;

using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Messages;
using RedisMessages.Metrics;
using RedisMessages.Options;

using StackExchange.Redis;

public class RedisCheckboxUpdatePublisherService : IHostedService, IRedisCheckboxUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly ActionBlock<CheckboxUpdateMessage> _publisherBlock;
    private readonly string _redisConnectionString;

    #endregion

    #region Constructors and Destructors

    public RedisCheckboxUpdatePublisherService(IOptions<RedisMessagePublisherOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
        _publisherBlock = new ActionBlock<CheckboxUpdateMessage>(
            PublishCheckboxUpdateMessages,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 10,
                BoundedCapacity = 1000
            }
        );
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishCheckboxUpdateAsync(string id, int index, bool value)
    {
        CheckboxUpdatePublisherManagerMetrics.QueuedMessageCount.Add(1);

        try
        {
            await _publisherBlock.SendAsync(
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

        finally
        {
            CheckboxUpdatePublisherManagerMetrics.QueuedMessageCount.Add(-1);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString, c => c.AbortOnConnectFail = false);
        _redisSubscriber = _redisConnection.GetSubscriber();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _publisherBlock.Complete();
        await _publisherBlock.Completion;

        if (_redisConnection != null)
        {
            await _redisConnection.DisposeAsync();
        }
    }

    #endregion

    #region Methods

    private async Task PublishCheckboxUpdateMessages(CheckboxUpdateMessage checkboxUpdateMessage)
    {
        if (_redisSubscriber == null)
        {
            return;
        }


        CheckboxUpdatePublisherManagerMetrics.ActiveWorkers.Add(1);
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var serializedCheckboxUpdate = JsonSerializer.Serialize(checkboxUpdateMessage.CheckboxUpdate);
            await _redisSubscriber.PublishAsync(new RedisChannel($"CheckboxUpdate:{checkboxUpdateMessage.Id}", RedisChannel.PatternMode.Literal), serializedCheckboxUpdate);
            CheckboxUpdatePublisherManagerMetrics.MessagePublishCount.Add(1);
        }

        catch
        {
            CheckboxUpdatePublisherManagerMetrics.MessageFailCount.Add(1);
            throw;
        }

        finally
        {
            stopwatch.Stop();
            CheckboxUpdatePublisherManagerMetrics.ActiveWorkers.Add(-1);
            CheckboxUpdatePublisherManagerMetrics.MessagePublishLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion
}
