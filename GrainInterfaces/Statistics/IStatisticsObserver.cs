namespace GrainInterfaces.Statistics;

using GrainInterfaces.Statistics.Models;

public interface IStatisticsObserver : IGrainObserver
{
    #region Public Methods and Operators

    public Task UpdateCheckboxStatisticsAsync(string id, CheckboxStatistics checkboxStatistics);

    #endregion
}
