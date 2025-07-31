namespace RedisMessages.CheckboxUpdate;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.CheckboxUpdate.Models;
using RedisMessages.Options;
using RedisMessages.RedisPublisher;

public class RedisCheckboxUpdatePublisherService : RedisPublisherBase, IRedisCheckboxUpdatePublisherManager
{
    #region Constructors and Destructors

    public RedisCheckboxUpdatePublisherService(ILogger<RedisCheckboxUpdatePublisherService> logger, IOptions<RedisPubSubOptions> options)
        : base(logger, options)
    {
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishCheckboxUpdateAsync(string id, int index, bool value)
    {
        await PublishJsonAsync($"CheckboxUpdate:{id}", new CheckboxUpdate
        {
            Index = index,
            Value = (byte)(value ? 1 : 0)
        });
    }

    public async Task PublishGoldSpotAsync(string id, int index)
    {
        await PublishJsonAsync($"GoldSpot:{id}", index);
    }

    #endregion
}
