namespace WarHubv1.WarObserver;

public interface IWarObserverManager
{
    #region Public Methods and Operators

    public Task SubscribeAsync(long id);
    public Task UnsubscribeAsync(long id);

    #endregion
}
