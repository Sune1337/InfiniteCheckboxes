namespace HighscoreGrain;

using GrainInterfaces.Highscore;

using Orleans.Concurrency;

[StatelessWorker]
public class HighscoreCollectorGrain : Grain, IHighscoreCollectorGrain
{
    #region Fields

    private readonly Dictionary<string, ulong> _scores = new();

    private string _grainId = null!;
    private IGrainTimer? _grainTimer;

    #endregion

    #region Public Methods and Operators

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

    public Task UpdateScore(string userId, ulong score)
    {
        if (score == 0)
        {
            return Task.CompletedTask;
        }

        _scores[userId] = score;
        return Task.CompletedTask;
    }

    #endregion

    #region Methods

    private async Task ReportStatistics()
    {
        if (_scores.Count > 0)
        {
            var highscoreGrain = GrainFactory.GetGrain<IHighscoreGrain>(_grainId);
            await highscoreGrain.UpdateScore(_scores);
            _scores.Clear();
        }
    }

    #endregion
}
