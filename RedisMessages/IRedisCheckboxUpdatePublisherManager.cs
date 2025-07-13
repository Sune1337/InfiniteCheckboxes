namespace RedisMessages;

public interface IRedisCheckboxUpdatePublisherManager
{
    #region Public Methods and Operators

    public Task PublishCheckboxUpdateAsync(string id, int index, bool value);

    #endregion
}
