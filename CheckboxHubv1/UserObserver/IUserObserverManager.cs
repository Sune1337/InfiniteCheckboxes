namespace CheckboxHubv1.UserObserver;

public interface IUserObserverManager
{
    #region Public Methods and Operators

    public Task SubscribeAsync(string id);
    public Task UnsubscribeAsync(string id);

    #endregion
}
