namespace CheckboxHubv1.Statistics;

using GrainInterfaces.Statistics.Models;

public interface IStatisticsObserverManager
{
    #region Public Methods and Operators

    public Task AddCheckboxSubscribers(string id, int count);
    public CheckboxCounters? GetCheckboxCounters();

    #endregion
}
