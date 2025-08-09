namespace RedisMessages.LightsOutUpdate;

using GrainInterfaces.LightsOut.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisPublisher;

public class RedisLightsOutUpdatePublisherService : RedisPublisherBase, IRedisLightsOutUpdatePublisherManager
{
    #region Constructors and Destructors

    public RedisLightsOutUpdatePublisherService(ILogger<RedisLightsOutUpdatePublisherService> logger, IOptions<RedisPubSubOptions> options)
        : base(logger, options)
    {
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishLightsOutUpdateAsync(string id, LightsOut lightsOut)
    {
        await PublishJsonAsync($"LightsOutUpdate:{id}", lightsOut);
    }

    #endregion
}
