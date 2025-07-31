namespace CheckboxHubv1.UserObserver;

using System.Text.Json;

using CheckboxHubv1.Hubs;

using GrainInterfaces.User.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisObserver;

using StackExchange.Redis;

using ValueDebouncer;

public class UserUpdates(ILogger logger)
{
    #region Fields

    public readonly DebounceValues<string, User> DebounceValues = new(logger);

    #endregion
}

public class UserObserverService : RedisObserverBase<UserUpdates>, IUserObserverManager
{
    #region Fields

    private readonly IHubContext<CheckboxHub> _checkboxHubContext;
    private readonly ILogger _logger;

    #endregion

    #region Constructors and Destructors

    public UserObserverService(ILogger<UserObserverService> logger, IOptions<RedisPubSubOptions> options, IHubContext<CheckboxHub> checkboxHubContext)
        : base(logger, options)
    {
        if (options.Value.RedisConnectionString == null)
        {
            throw new ArgumentNullException(nameof(options.Value.RedisConnectionString), "Redis connection string is null.");
        }

        _logger = logger;
        _checkboxHubContext = checkboxHubContext;
    }

    #endregion

    #region Public Methods and Operators

    public async Task SubscribeAsync(string id)
    {
        await SubscribeTopicAsync($"UserUpdate:{id}");
    }

    public async Task UnsubscribeAsync(string id)
    {
        await UnsubscribeTopicAsync($"UserUpdate:{id}");
    }

    #endregion

    #region Methods

    protected override UserUpdates CreateState(string topic)
    {
        var key = topic.Split(':');
        if (key.Length != 2)
        {
            throw new ArgumentException("Topic must be in the format {topic}:{id}.");
        }

        var userUpdates = new UserUpdates(_logger);
        userUpdates.DebounceValues.EmitValues += async values =>
        {
            foreach (var value in values.Values)
            {
                await _checkboxHubContext.Clients
                    .User(key[1])
                    .SendAsync("UB", new { value.GoldBalance });
            }
        };

        return userUpdates;
    }

    protected override void DestroyState(string topic, UserUpdates? state)
    {
        state?.DebounceValues.UnregisterEmitters();
    }

    protected override void WhenRedisMessageReceived(RedisChannel redisChannel, RedisValue redisValue, UserUpdates? state)
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
            case "UserUpdate":
                var user = JsonSerializer.Deserialize<User>(redisValueAsString);
                if (user == null)
                {
                    return;
                }

                state.DebounceValues.DebounceValue(key[1], user);
                break;
        }
    }

    #endregion
}
