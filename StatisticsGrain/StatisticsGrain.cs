namespace StatisticsGrain;

using global::StatisticsGrain.Models;

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
    private readonly DebounceValues<StatisticsState> _globalStatisticsDebouncer;
    private readonly ObserverManager<IStatisticsObserver> _statisticsObserverManager;
    private readonly IPersistentState<StatisticsState> _statisticsState;

    #endregion

    #region Constructors and Destructors

    public StatisticsGrain(
        ILogger<StatisticsGrain> logger,
        [PersistentState("StatisticsState", "StatisticsStore")]
        IPersistentState<StatisticsState> statisticsState
    )
    {
        _statisticsObserverManager = new ObserverManager<IStatisticsObserver>(TimeSpan.FromMinutes(5), logger);
        _checkboxStatisticsDebouncer = new DebounceValues<string, CheckboxStatistics>(logger);
        _globalStatisticsDebouncer = new DebounceValues<StatisticsState>(logger, 1000);
        _statisticsState = statisticsState;
    }

    #endregion

    #region Public Methods and Operators

    public async Task AddCheckboxCounters(int countChecked, int countUnchecked)
    {
        _statisticsState.State.CountChecked += (ulong)countChecked;
        _statisticsState.State.CountUnchecked += (ulong)countUnchecked;

        if (_statisticsState.State.CountUnchecked > _statisticsState.State.CountChecked)
        {
            _statisticsState.State.CountChecked = _statisticsState.State.CountUnchecked;
        }

        await _statisticsState.WriteStateAsync();
        _globalStatisticsDebouncer.DebounceValue(_statisticsState.State);
    }

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

    public Task<CheckboxCounters> GetCheckboxCounters()
    {
        return Task.FromResult(new CheckboxCounters
        {
            NumberOfChecked = _statisticsState.State.CountChecked,
            NumberOfUnchecked = _statisticsState.State.CountUnchecked
        });
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _checkboxStatisticsDebouncer.EmitValues += EmitCheckboxStatistics;
        _globalStatisticsDebouncer.EmitValue += EmitGlobalStatistics;
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _checkboxStatisticsDebouncer.EmitValues -= EmitCheckboxStatistics;
        _globalStatisticsDebouncer.EmitValue -= EmitGlobalStatistics;
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

    private async Task EmitGlobalStatistics(StatisticsState value)
    {
        await _statisticsObserverManager.Notify(c => c.UpdateGlobalStatisticsAsync(value.CountChecked, value.CountUnchecked));
    }

    #endregion
}
