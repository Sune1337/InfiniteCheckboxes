namespace RedisMessages.CheckboxUpdate;

public interface IRedisCheckboxUpdatePublisherManager
{
    #region Public Methods and Operators

    public Task PublishCheckboxUpdateAsync(string id, int index, bool value);
    public Task PublishGoldSpotAsync(string id, int index);

    #endregion
}
