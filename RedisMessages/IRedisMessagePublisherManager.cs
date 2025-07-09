namespace RedisMessages;

public interface IRedisMessagePublisherManager
{
    #region Public Methods and Operators

    public Task PublishCheckboxUpdateAsync(string id, int index, byte value);

    #endregion
}
