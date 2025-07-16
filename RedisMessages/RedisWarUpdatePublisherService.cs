namespace RedisMessages;

using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

using GrainInterfaces.War.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RedisMessages.Messages;
using RedisMessages.Metrics;
using RedisMessages.Options;

using StackExchange.Redis;

public class RedisWarUpdatePublisherService : IHostedService, IRedisWarUpdatePublisherManager
{
    #region Static Fields

    private static ConnectionMultiplexer? _redisConnection;
    private static ISubscriber? _redisSubscriber;

    #endregion

    #region Fields

    private readonly ActionBlock<WarUpdateMessage> _publisherBlock;
    private readonly string _redisConnectionString;

    #endregion

    #region Constructors and Destructors

    public RedisWarUpdatePublisherService(IOptions<RedisMessagePublisherOptions> options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _redisConnectionString = options.Value.RedisConnectionString;
        _publisherBlock = new ActionBlock<WarUpdateMessage>(
            PublishWarUpdateMessages,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 10,
                BoundedCapacity = 1000
            }
        );
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishWarUpdateAsync(long id, War war)
    {
        WarUpdatePublisherManagerMetrics.QueuedMessageCount.Add(1);

        try
        {
            await _publisherBlock.SendAsync(
                new WarUpdateMessage
                {
                    Id = id,
                    War = war
                }
            );
        }

        finally
        {
            WarUpdatePublisherManagerMetrics.QueuedMessageCount.Add(-1);
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

    private async Task PublishWarUpdateMessages(WarUpdateMessage warUpdateMessage)
    {
        if (_redisSubscriber == null)
        {
            return;
        }


        WarUpdatePublisherManagerMetrics.ActiveWorkers.Add(1);
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        try
        {
            var serializedWarUpdate = JsonSerializer.Serialize(warUpdateMessage.War);
            await _redisSubscriber.PublishAsync(new RedisChannel($"WarUpdate:{warUpdateMessage.Id}", RedisChannel.PatternMode.Literal), serializedWarUpdate);
            WarUpdatePublisherManagerMetrics.MessagePublishCount.Add(1);
        }

        catch
        {
            WarUpdatePublisherManagerMetrics.MessageFailCount.Add(1);
            throw;
        }

        finally
        {
            stopwatch.Stop();
            WarUpdatePublisherManagerMetrics.ActiveWorkers.Add(-1);
            WarUpdatePublisherManagerMetrics.MessagePublishLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion
}
