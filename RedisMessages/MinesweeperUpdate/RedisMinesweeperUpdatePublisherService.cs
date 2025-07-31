namespace RedisMessages.MinesweeperUpdate;

using GrainInterfaces.Minesweeper.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RedisMessages.Options;
using RedisMessages.RedisPublisher;

public class RedisMinesweeperUpdatePublisherService : RedisPublisherBase, IRedisMinesweeperUpdatePublisherManager
{
    #region Constructors and Destructors

    public RedisMinesweeperUpdatePublisherService(ILogger<RedisMinesweeperUpdatePublisherService> logger, IOptions<RedisPubSubOptions> options)
        : base(logger, options)
    {
    }

    #endregion

    #region Public Methods and Operators

    public async Task PublishCountsAsync(string id, Dictionary<int, int> counts)
    {
        await PublishJsonAsync($"MinesweeperCounts:{id}", counts);
    }

    public async Task PublishMinesweeperAsync(string id, Minesweeper minesweeper)
    {
        await PublishJsonAsync($"MinesweeperUpdate:{id}", minesweeper);
    }

    #endregion
}
