namespace CheckboxHubv1.CheckboxObservers;

public interface ICheckboxObserverManager
{
    #region Public Methods and Operators

    public Task SubscribeAsync(string id);
    public Task UnsubscribeAsync(string id);

    #endregion
}
