namespace RedisMessages.WarUpdate;

using GrainInterfaces.War.Models;

public interface IRedisWarUpdatePublisherManager
{
    #region Public Methods and Operators

    public Task PublishWarUpdateAsync(long id, War war);

    #endregion
}
