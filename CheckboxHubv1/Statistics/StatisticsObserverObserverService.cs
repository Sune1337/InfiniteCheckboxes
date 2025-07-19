namespace CheckboxHubv1.Statistics;

using CheckboxHubv1.Hubs;

using GrainInterfaces.Statistics;
using GrainInterfaces.Statistics.Models;

using Microsoft.AspNetCore.SignalR;

using Two56bitId;

public class StatisticsObserverObserverService : IHostedService, IStatisticsObserverManager, IStatisticsObserver
{
    #region Fields

    private readonly IHubContext<CheckboxHub> _checkboxHubContext;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<StatisticsObserverObserverService> _logger;
    private readonly CancellationTokenSource _subscribeStatisticsTaskCancellationToken = new();

    private CheckboxCounters? _checkboxCounters;
    private Task? _subscribeStatisticsTask;

    #endregion

    #region Constructors and Destructors

    public StatisticsObserverObserverService(ILogger<StatisticsObserverObserverService> logger, IGrainFactory grainFactory, IHubContext<CheckboxHub> checkboxHubContext)
    {
        _logger = logger;
        _grainFactory = grainFactory;
        _checkboxHubContext = checkboxHubContext;
    }

    #endregion

    #region Public Methods and Operators

    public async Task AddCheckboxSubscribers(string id, int count)
    {
        var statisticsGrain = _grainFactory.GetGrain<IStatisticsGrain>(0);
        await statisticsGrain.AddCheckboxSubscribers(id, count);
    }

    public CheckboxCounters? GetCheckboxCounters()
    {
        return _checkboxCounters;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscribeStatisticsTask = SubscribeStatisticsTask(_subscribeStatisticsTaskCancellationToken.Token);

        // Get initial global statistics.
        var statisticsGrain = _grainFactory.GetGrain<IStatisticsGrain>(0);
        _checkboxCounters = await statisticsGrain.GetCheckboxCounters();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscribeStatisticsTask != null)
        {
            await _subscribeStatisticsTaskCancellationToken.CancelAsync();
            await _subscribeStatisticsTask;
        }
    }

    public async Task UpdateCheckboxStatisticsAsync(string id, CheckboxStatistics checkboxStatistics)
    {
        var base64Id = Convert.ToBase64String(id.HexStringToByteArray());
        await _checkboxHubContext.Clients
            .Group($"{HubGroups.CheckboxGroupPrefix}_{id}")
            .SendAsync("CS", base64Id, checkboxStatistics);
    }

    public async Task UpdateGlobalStatisticsAsync(ulong countChecked, ulong countUnchecked)
    {
        _checkboxCounters ??= new CheckboxCounters();
        _checkboxCounters.NumberOfChecked = countChecked;
        _checkboxCounters.NumberOfUnchecked = countUnchecked;

        var totalChecked = countUnchecked > countChecked ? 0 : countChecked - countUnchecked;
        await _checkboxHubContext.Clients
            .All
            .SendAsync("GS", totalChecked);
    }

    #endregion

    #region Methods

    private async Task SubscribeStatisticsTask(CancellationToken cancellationToken)
    {
        IStatisticsGrain? statisticsGrain;
        var observerReference = _grainFactory.CreateObjectReference<IStatisticsObserver>(this);

        try
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    statisticsGrain = _grainFactory.GetGrain<IStatisticsGrain>(0);
                    await statisticsGrain.Subscribe(observerReference);
                }

                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception caught in SubscribeStatisticsTask: {ExceptionMessage}.", ex.Message);
                }

                // Wait a period and subscribe again.
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        catch (OperationCanceledException)
        {
            // Stop reading the channel.
        }


        statisticsGrain = _grainFactory.GetGrain<IStatisticsGrain>(0);
        await statisticsGrain.UnSubscribe(observerReference);
    }

    #endregion
}
