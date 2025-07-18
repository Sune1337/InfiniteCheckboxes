namespace GrainInterfaces.Statistics;

public interface IStatisticsGrain : IGrainWithIntegerKey
{
    #region Public Methods and Operators

    public Task AddCheckboxSubscribers(string id, int count);
    public Task Subscribe(IStatisticsObserver observer);
    public Task UnSubscribe(IStatisticsObserver observer);

    #endregion
}
