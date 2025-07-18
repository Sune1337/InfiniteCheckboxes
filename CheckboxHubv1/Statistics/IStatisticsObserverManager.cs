namespace CheckboxHubv1.Statistics;

public interface IStatisticsObserverManager
{
    #region Public Methods and Operators

    public Task AddCheckboxSubscribers(string id, int count);

    #endregion
}
