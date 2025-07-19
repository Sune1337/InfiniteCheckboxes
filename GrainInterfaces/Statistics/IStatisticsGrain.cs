namespace GrainInterfaces.Statistics;

using GrainInterfaces.Statistics.Models;

public interface IStatisticsGrain : IGrainWithIntegerKey
{
    #region Public Methods and Operators

    public Task AddCheckboxCounters(int countChecked, int countUnchecked);
    public Task AddCheckboxSubscribers(string id, int count);
    public Task<CheckboxCounters> GetCheckboxCounters();
    public Task Subscribe(IStatisticsObserver observer);
    public Task UnSubscribe(IStatisticsObserver observer);

    #endregion
}
