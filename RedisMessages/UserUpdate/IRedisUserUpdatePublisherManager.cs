namespace RedisMessages.UserUpdate;

using GrainInterfaces.User.Models;

public interface IRedisUserUpdatePublisherManager
{
    #region Public Methods and Operators

    public Task PublishUserUpdateAsync(string id, User user);

    #endregion
}
