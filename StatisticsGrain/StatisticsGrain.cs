namespace StatisticsGrain;

using GrainInterfaces.Statistics;
using GrainInterfaces.Statistics.Models;

using Microsoft.Extensions.Logging;

using Orleans.Utilities;

using ValueDebouncer;

public class StatisticsGrain : Grain, IStatisticsGrain
{
    #region Fields

    private readonly Dictionary<string, CheckboxStatistics> _checkboxStatistics = new();
    private readonly DebounceValues<string, CheckboxStatistics> _checkboxStatisticsDebouncer;
    private readonly ObserverManager<IStatisticsObserver> _statisticsObserverManager;

    #endregion

    #region Constructors and Destructors

    public StatisticsGrain(ILogger<StatisticsGrain> logger)
    {
        _statisticsObserverManager = new ObserverManager<IStatisticsObserver>(TimeSpan.FromMinutes(5), logger);
        _checkboxStatisticsDebouncer = new DebounceValues<string, CheckboxStatistics>(logger);
    }

    #endregion

    #region Public Methods and Operators

    public Task AddCheckboxSubscribers(string id, int count)
    {
        if (_checkboxStatistics.TryGetValue(id, out var checkboxStatistics) == false)
        {
            checkboxStatistics = new CheckboxStatistics();
            _checkboxStatistics[id] = checkboxStatistics;
        }

        checkboxStatistics.NumberOfSubscribers += count;
        if (checkboxStatistics.NumberOfSubscribers <= 0)
        {
            // Set to 0 for publishing.
            checkboxStatistics.NumberOfSubscribers = 0;
            
            // And remove from dictionary.
            _checkboxStatistics.Remove(id);
        }

        _checkboxStatisticsDebouncer.DebounceValue(id, checkboxStatistics);

        return Task.CompletedTask;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _checkboxStatisticsDebouncer.EmitValues += EmitCheckboxStatistics;
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _checkboxStatisticsDebouncer.EmitValues -= EmitCheckboxStatistics;
        return Task.CompletedTask;
    }

    public Task Subscribe(IStatisticsObserver observer)
    {
        _statisticsObserverManager.Subscribe(observer, observer);
        return Task.CompletedTask;
    }

    public Task UnSubscribe(IStatisticsObserver observer)
    {
        _statisticsObserverManager.Unsubscribe(observer);
        return Task.CompletedTask;
    }

    #endregion

    #region Methods

    private async Task EmitCheckboxStatistics(Dictionary<string, CheckboxStatistics> values)
    {
        foreach (var value in values)
        {
            await _statisticsObserverManager.Notify(c => c.UpdateCheckboxStatisticsAsync(value.Key, value.Value));
        }
    }

    #endregion
}
