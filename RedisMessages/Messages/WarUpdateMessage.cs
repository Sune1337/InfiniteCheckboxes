namespace RedisMessages.Messages;

using GrainInterfaces.War.Models;

public class WarUpdateMessage
{
    #region Public Properties

    public required long Id { get; set; }
    public required War War { get; set; }

    #endregion
}
