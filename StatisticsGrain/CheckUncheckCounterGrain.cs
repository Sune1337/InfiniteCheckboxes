namespace StatisticsGrain;

using GrainInterfaces.Statistics;

using Orleans.Concurrency;

[StatelessWorker]
public class CheckUncheckCounterGrain : Grain, ICheckUncheckCounter
{
    #region Fields

    private readonly IGrainFactory _grainFactory;

    private int _countChecked;
    private int _countUnchecked;
    private IGrainTimer? _grainTimer;

    #endregion

    #region Constructors and Destructors

    public CheckUncheckCounterGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    #endregion

    #region Public Methods and Operators

    public Task AddCheckUncheck(int countChecked, int countUnchecked)
    {
        _countChecked += countChecked;
        _countUnchecked += countUnchecked;
        return Task.CompletedTask;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainTimer = this.RegisterGrainTimer(ReportStatistics, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        return Task.CompletedTask;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _grainTimer?.Dispose();
        await ReportStatistics();
    }

    #endregion

    #region Methods

    private async Task ReportStatistics()
    {
        if (_countChecked > 0 || _countUnchecked > 0)
        {
            var statisticsGrain = _grainFactory.GetGrain<IStatisticsGrain>(0);
            await statisticsGrain.AddCheckboxCounters(_countChecked, _countUnchecked);
            _countChecked = 0;
            _countUnchecked = 0;
        }
    }

    #endregion
}
