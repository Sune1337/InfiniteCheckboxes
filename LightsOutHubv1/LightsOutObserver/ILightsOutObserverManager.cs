namespace LightsOutHubv1.LightsOutObserver;

public interface ILightsOutObserverManager
{
    #region Public Methods and Operators

    public Task SubscribeAsync(string id);
    public Task UnsubscribeAsync(string id);

    #endregion
}
