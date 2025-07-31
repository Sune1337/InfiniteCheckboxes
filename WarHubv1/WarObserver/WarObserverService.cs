namespace WarHubv1.WarObserver;

using System.Text.Json;

using GrainInterfaces.War.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisObserver;

using StackExchange.Redis;

using ValueDebouncer;

using WarHubv1.Hubs;

public class WarUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<War> DebounceValues = new(logger);

    #endregion
}

public class WarObserverService : RedisObserverBase<WarUpdates>, IWarObserverManager
{
    #region Fields

    private readonly ILogger _logger;
    private readonly IHubContext<WarHub> _warHubContext;

    #endregion

    #region Constructors and Destructors

    public WarObserverService(ILogger<WarObserverService> logger, IOptions<RedisPubSubOptions> options, IHubContext<WarHub> warHubContext)
        : base(logger, options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _warHubContext = warHubContext;
        _logger = logger;
    }

    #endregion

    #region Public Methods and Operators

    public async Task SubscribeAsync(long id)
    {
        await SubscribeTopicAsync($"WarUpdate:{id}");
    }

    public async Task UnsubscribeAsync(long id)
    {
        await UnsubscribeTopicAsync($"WarUpdate:{id}");
    }

    #endregion

    #region Methods

    protected override WarUpdates CreateState(string topic)
    {
        var key = topic.Split(':');
        if (key.Length != 2)
        {
            throw new ArgumentException("Topic must be in the format {topic}:{id}.");
        }

        var warUpdates = new WarUpdates(_logger);

        warUpdates.DebounceValues.EmitValue += async value =>
        {
            await _warHubContext.Clients
                .Group($"{HubGroups.WarGroupPrefix}_{key[1]}")
                .SendAsync("WarsUpdate", key[1], value);
        };

        return warUpdates;
    }

    protected override void DestroyState(string topic, WarUpdates? state)
    {
        state?.DebounceValues.UnregisterEmitters();
    }

    protected override void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue, WarUpdates? state)
    {
        if (state == null || !redisValue.HasValue)
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
            case "WarUpdate":
                var warUpdate = JsonSerializer.Deserialize<War>(redisValueAsString);
                if (warUpdate == null)
                {
                    return;
                }

                state.DebounceValues.DebounceValue(warUpdate);
                break;
        }
    }

    #endregion
}
