namespace CheckboxHubv1.CheckboxObserver;

public interface ICheckboxObserverManager
{
    #region Public Methods and Operators

    public Task SubscribeAsync(string id);
    public Task UnsubscribeAsync(string id);

    #endregion
}
