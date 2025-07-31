namespace RedisMessages.WarUpdate;

using GrainInterfaces.War.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisPublisher;

public class RedisWarUpdatePublisherService : RedisPublisherBase, IRedisWarUpdatePublisherManager
{
    #region Constructors and Destructors

    public RedisWarUpdatePublisherService(ILogger<RedisWarUpdatePublisherService> logger, IOptions<RedisPubSubOptions> options)
        : base(logger, options)
    {
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishWarUpdateAsync(long id, War war)
    {
        await PublishJsonAsync($"WarUpdate:{id}", war);
    }

    #endregion
}
