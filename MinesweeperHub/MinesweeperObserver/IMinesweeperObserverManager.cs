namespace MinesweeperHub.MinesweeperObserver;

public interface IMinesweeperObserverManager
{
    #region Public Methods and Operators

    public Task SubscribeAsync(string id);
    public Task UnsubscribeAsync(string id);

    #endregion
}
