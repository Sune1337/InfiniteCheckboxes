namespace LightsOutHubv1.LightsOutObserver;

using System.Text.Json;

using GrainInterfaces.LightsOut.Models;

using LightsOutHubv1.Hubs;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisObserver;

using StackExchange.Redis;

using Two56bitId;

using ValueDebouncer;

public class LightsOutUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<LightsOut> DebounceValues = new(logger);

    #endregion
}

public class LightsOutObserverService : RedisObserverBase<LightsOutUpdates>, ILightsOutObserverManager
{
    #region Fields

    private readonly IHubContext<LightsOutHub> _lightsOutHubContext;

    private readonly ILogger _logger;

    #endregion

    #region Constructors and Destructors

    public LightsOutObserverService(ILogger<LightsOutObserverService> logger, IOptions<RedisPubSubOptions> options, IHubContext<LightsOutHub> lightsOutHubContext)
        : base(logger, options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _lightsOutHubContext = lightsOutHubContext;
        _logger = logger;
    }

    #endregion

    #region Public Methods and Operators

    public async Task SubscribeAsync(string id)
    {
        await SubscribeTopicAsync($"LightsOutUpdate:{id}");
    }

    public async Task UnsubscribeAsync(string id)
    {
        await UnsubscribeTopicAsync($"LightsOutUpdate:{id}");
    }

    #endregion

    #region Methods

    protected override LightsOutUpdates CreateState(string topic)
    {
        var key = topic.Split(':');
        if (key.Length != 2)
        {
            throw new ArgumentException("Topic must be in the format {topic}:{id}.");
        }

        var byteId = key[1].HexStringToByteArray();
        var lightsOutUpdates = new LightsOutUpdates(_logger);

        lightsOutUpdates.DebounceValues.EmitValue += async value =>
        {
            await _lightsOutHubContext.Clients
                .Group($"{HubGroups.LightsOutGroupPrefix}_{key[1]}")
                .SendAsync("LightsOutUpdate", byteId, value);
        };

        return lightsOutUpdates;
    }

    protected override void DestroyState(string topic, LightsOutUpdates? state)
    {
        state?.DebounceValues.UnregisterEmitters();
    }

    protected override void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue, LightsOutUpdates? state)
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
            case "LightsOutUpdate":
                var lightsOutUpdate = JsonSerializer.Deserialize<LightsOut>(redisValueAsString);
                if (lightsOutUpdate == null)
                {
                    return;
                }

                state.DebounceValues.DebounceValue(lightsOutUpdate);
                break;
        }
    }

    #endregion
}
