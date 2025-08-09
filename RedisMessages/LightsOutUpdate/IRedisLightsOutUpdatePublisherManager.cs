namespace RedisMessages.LightsOutUpdate;

using GrainInterfaces.LightsOut.Models;

public interface IRedisLightsOutUpdatePublisherManager
{
    #region Public Methods and Operators

    public Task PublishLightsOutUpdateAsync(string id, LightsOut lightsOut);

    #endregion
}
