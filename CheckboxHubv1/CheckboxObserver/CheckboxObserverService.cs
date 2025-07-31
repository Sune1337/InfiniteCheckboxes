namespace CheckboxHubv1.CheckboxObserver;

using System.Text.Json;

using CheckboxHubv1.Hubs;
using CheckboxHubv1.Statistics;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using RedisMessages.CheckboxUpdate.Models;
using RedisMessages.Options;
using RedisMessages.RedisObserver;

using StackExchange.Redis;

using Two56bitId;

using ValueDebouncer;

public class CheckboxPageUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<int, byte> DebounceCheckboxUpdate = new(logger);
    public readonly DebounceValues<int, bool> DebounceGoldSpot = new(logger);

    #endregion
}

public class CheckboxObserverService : RedisObserverBase<CheckboxPageUpdates>, ICheckboxObserverManager
{
    #region Fields

    private readonly IHubContext<CheckboxHub> _checkboxHubContext;
    private readonly ILogger _logger;
    private readonly IStatisticsObserverManager _statisticsObserverManager;

    #endregion

    #region Constructors and Destructors

    public CheckboxObserverService(ILogger<CheckboxObserverService> logger, IOptions<RedisPubSubOptions> options, IHubContext<CheckboxHub> checkboxHubContext, IStatisticsObserverManager statisticsObserverManager)
        : base(logger, options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _logger = logger;
        _checkboxHubContext = checkboxHubContext;
        _statisticsObserverManager = statisticsObserverManager;
    }

    #endregion

    #region Public Methods and Operators

    public async Task SubscribeAsync(string id)
    {
        await SubscribeTopicAsync($"CheckboxUpdate:{id}");
        await SubscribeTopicAsync($"GoldSpot:{id}");
        await _statisticsObserverManager.AddCheckboxSubscribers(id, 1);
    }

    public async Task UnsubscribeAsync(string id)
    {
        await UnsubscribeTopicAsync($"CheckboxUpdate:{id}");
        await UnsubscribeTopicAsync($"GoldSpot:{id}");
        await _statisticsObserverManager.AddCheckboxSubscribers(id, -1);
    }

    #endregion

    #region Methods

    protected override CheckboxPageUpdates CreateState(string topic)
    {
        var key = topic.Split(':');
        if (key.Length != 2)
        {
            throw new ArgumentException("Topic must be in the format {topic}:{id}.");
        }

        var byteId = key[1].HexStringToByteArray();
        var checkboxPageUpdates = new CheckboxPageUpdates(_logger);

        checkboxPageUpdates.DebounceCheckboxUpdate.EmitValues += async values =>
        {
            await _checkboxHubContext.Clients
                .Group($"{HubGroups.CheckboxGroupPrefix}_{key[1]}")
                .SendAsync("CheckboxesUpdate", byteId, BitCoding.IndexAndBoolCoder.Encode(values));
        };
        checkboxPageUpdates.DebounceGoldSpot.EmitValues += async values =>
        {
            await _checkboxHubContext.Clients
                .Group($"{HubGroups.CheckboxGroupPrefix}_{key[1]}")
                .SendAsync("GoldSpot", byteId, values.Keys.ToArray());
        };

        return checkboxPageUpdates;
    }

    protected override void DestroyState(string topic, CheckboxPageUpdates? state)
    {
        state?.DebounceCheckboxUpdate.UnregisterEmitters();
        state?.DebounceGoldSpot.UnregisterEmitters();
    }

    protected override void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue, CheckboxPageUpdates? state)
    {
        if (state == null || !redisValue.HasValue)
        {
            return;
        }

        var key = redisChannel.ToString().Split(':');
        if (key.Length != 2)
        {
            return;
        }

        switch (key[0])
        {
            case "CheckboxUpdate":
                var redisValueAsString = redisValue.ToString();
                var checkboxUpdate = JsonSerializer.Deserialize<CheckboxUpdate>(redisValueAsString);
                if (checkboxUpdate == null)
                {
                    return;
                }

                state.DebounceCheckboxUpdate.DebounceValue(checkboxUpdate.Index, checkboxUpdate.Value);
                break;

            case "GoldSpot":
                if (redisValue.TryParse(out int redisValueAsInt) == false)
                {
                    break;
                }

                state.DebounceGoldSpot.DebounceValue(redisValueAsInt, true);
                break;
        }
    }

    #endregion
}
