namespace RedisMessages.UserUpdate;

using GrainInterfaces.User.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisPublisher;

public class RedisUserUpdatePublisherService : RedisPublisherBase, IRedisUserUpdatePublisherManager
{
    #region Constructors and Destructors

    public RedisUserUpdatePublisherService(ILogger<RedisUserUpdatePublisherService> logger, IOptions<RedisPubSubOptions> options)
        : base(logger, options)
    {
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishUserUpdateAsync(string id, User user)
    {
        await PublishJsonAsync($"UserUpdate:{id}", user);
    }

    #endregion
}
