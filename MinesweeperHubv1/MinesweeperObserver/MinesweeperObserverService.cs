namespace MinesweeperHubv1.MinesweeperObserver;

using System.Text.Json;

using GrainInterfaces.Minesweeper.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using MinesweeperHubv1.Hubs;

using RedisMessages.Options;
using RedisMessages.RedisObserver;

using StackExchange.Redis;

using Two56bitId;

using ValueDebouncer;

public class MinesweeperUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<string, Dictionary<int, int>> DebounceCounts = new(logger, 0);
    public readonly DebounceValues<string, Minesweeper> DebounceMinesweeper = new(logger);

    #endregion
}

public class MinesweeperObserverService : RedisObserverBase<MinesweeperUpdates>, IMinesweeperObserverManager
{
    #region Fields

    private readonly ILogger _logger;
    private readonly IHubContext<MinesweeperHub> _minesweeperHubContext;

    #endregion

    #region Constructors and Destructors

    public MinesweeperObserverService(ILogger<MinesweeperObserverService> logger, IOptions<RedisPubSubOptions> options, IHubContext<MinesweeperHub> minesweeperHubContext)
        : base(logger, options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _minesweeperHubContext = minesweeperHubContext;
        _logger = logger;
    }

    #endregion

    #region Public Methods and Operators

    public async Task SubscribeAsync(string id)
    {
        await SubscribeTopicAsync($"MinesweeperUpdate:{id}");
        await SubscribeTopicAsync($"MinesweeperCounts:{id}");
    }

    public async Task UnsubscribeAsync(string id)
    {
        await UnsubscribeTopicAsync($"MinesweeperUpdate:{id}");
        await UnsubscribeTopicAsync($"MinesweeperCounts:{id}");
    }

    #endregion

    #region Methods

    protected override MinesweeperUpdates CreateState(string topic)
    {
        var key = topic.Split(':');
        if (key.Length != 2)
        {
            throw new ArgumentException("Topic must be in the format {topic}:{id}.");
        }

        var byteId = key[1].HexStringToByteArray();
        var minesweeperUpdates = new MinesweeperUpdates(_logger);

        minesweeperUpdates.DebounceMinesweeper.EmitValues += async values =>
        {
            foreach (var value in values.Values)
            {
                await _minesweeperHubContext.Clients
                    .Group($"{HubGroups.MinesweeperGroupPrefix}_{key[1]}")
                    .SendAsync("MinesweeperUpdate", byteId, value);
            }
        };

        minesweeperUpdates.DebounceCounts.EmitValues += async values =>
        {
            foreach (var value in values.Values)
            {
                await _minesweeperHubContext.Clients
                    .Group($"{HubGroups.MinesweeperGroupPrefix}_{key[1]}")
                    .SendAsync("MinesweeperCounts", byteId, value);
            }
        };

        return minesweeperUpdates;
    }

    protected override void DestroyState(string topic, MinesweeperUpdates? state)
    {
        state?.DebounceMinesweeper.UnregisterEmitters();
        state?.DebounceCounts.UnregisterEmitters();
    }

    protected override void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue, MinesweeperUpdates? state)
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
            case "MinesweeperUpdate":
                var minesweeperUpdate = JsonSerializer.Deserialize<Minesweeper>(redisValueAsString);
                if (minesweeperUpdate == null)
                {
                    return;
                }

                state.DebounceMinesweeper.DebounceValue(key[1], minesweeperUpdate);
                break;

            case "MinesweeperCounts":
                var minesweeperCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(redisValueAsString);
                if (minesweeperCounts == null)
                {
                    return;
                }

                state.DebounceCounts.DebounceValue(key[1], minesweeperCounts);
                break;
        }
    }

    #endregion
}
