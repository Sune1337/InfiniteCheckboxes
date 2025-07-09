namespace RedisMessages.Messages;

public class CheckboxUpdateMessage
{
    #region Public Properties

    public required CheckboxUpdate CheckboxUpdate { get; set; }
    public required string Id { get; set; }

    #endregion
}
