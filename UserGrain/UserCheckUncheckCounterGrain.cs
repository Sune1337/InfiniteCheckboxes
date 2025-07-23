namespace UserGrain;

using GrainInterfaces.User;

using Orleans.Concurrency;

[StatelessWorker]
public class UserCheckUncheckCounterGrain : Grain, IUserCheckUncheckCounterGrain
{
    #region Fields

    private int _countChecked;
    private int _countUnchecked;
    private string _grainId = null!;
    private IGrainTimer? _grainTimer;

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
        _grainId = this.GetPrimaryKeyString();
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
            var userGrain = GrainFactory.GetGrain<IUserGrain>(_grainId);
            await userGrain.AddCheckUncheck(_countChecked, _countUnchecked);
            _countChecked = 0;
            _countUnchecked = 0;
        }
    }

    #endregion
}
