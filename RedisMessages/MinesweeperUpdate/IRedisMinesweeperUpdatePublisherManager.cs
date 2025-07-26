namespace RedisMessages.MinesweeperUpdate;

using GrainInterfaces.Minesweeper.Models;

public interface IRedisMinesweeperUpdatePublisherManager
{
    #region Public Methods and Operators

    public Task PublishCountsAsync(string id, Dictionary<int, int> counts);
    public Task PublishMinesweeperAsync(string id, Minesweeper minesweeper);

    #endregion
}
