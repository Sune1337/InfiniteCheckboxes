namespace CheckboxHubv1.Hubs;

using System.Security.Claims;
using System.Threading.RateLimiting;

using CheckboxHubv1.CheckboxObserver;
using CheckboxHubv1.Statistics;

using GrainInterfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using Two56bitId;

[Authorize]
public class CheckboxHub : Hub
{
    #region Fields

    private readonly ICheckboxObserverManager _checkboxObserverManager;
    private readonly IGrainFactory _grainFactory;
    private readonly IStatisticsObserverManager _statisticsObserverManager;

    #endregion

    #region Constructors and Destructors

    public CheckboxHub(IGrainFactory grainFactory, ICheckboxObserverManager checkboxObserverManager, IStatisticsObserverManager statisticsObserverManager)
    {
        _grainFactory = grainFactory;
        _checkboxObserverManager = checkboxObserverManager;
        _statisticsObserverManager = statisticsObserverManager;
    }

    #endregion

    #region Properties

    private HashSet<string>? CheckboxIds => Context.Items["CheckboxIds"] as HashSet<string>;
    private FixedWindowRateLimiter? FixedWindowRateLimiter => Context.Items["FixedWindowRateLimiter"] as FixedWindowRateLimiter;

    #endregion

    #region Public Methods and Operators

    public async Task<byte[]?> CheckboxesSubscribe(string base64Id)
    {
        if (base64Id.TryParse256BitBase64Id(out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(base64Id));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");
        await _checkboxObserverManager.SubscribeAsync(parsedId);

        // Remember that the current client subscribes to this checkbox-page.
        CheckboxIds?.Add(parsedId);

        // Get the initial state of checkboxes.
        var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(parsedId);
        return await checkboxGrain.GetCheckboxes();
    }

    public async Task CheckboxesUnsubscribe(string base64Id)
    {
        if (base64Id.TryParse256BitBase64Id(out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(base64Id));
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");
        await _checkboxObserverManager.UnsubscribeAsync(parsedId);

        // The current connection no longer subscribes to the checkbox-page.
        CheckboxIds?.Remove(parsedId);
    }

    public override async Task OnConnectedAsync()
    {
        // Create a list to keep track of which checkbox-pages the connection subscribes to.
        Context.Items.Add("CheckboxIds", new HashSet<string>());
        Context.Items.Add("FixedWindowRateLimiter", new FixedWindowRateLimiter(
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromSeconds(1)
            }
        ));

        // Get current checkbox-counters to seed client.
        var checkboxCounters = _statisticsObserverManager.GetCheckboxCounters();
        if (checkboxCounters != null)
        {
            var totalChecked = checkboxCounters.NumberOfUnchecked > checkboxCounters.NumberOfChecked ? 0 : checkboxCounters.NumberOfChecked - checkboxCounters.NumberOfUnchecked;
            await Clients.Caller.SendAsync("GS", totalChecked);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (CheckboxIds != null)
        {
            foreach (var id in CheckboxIds)
            {
                try
                {
                    await _checkboxObserverManager.UnsubscribeAsync(id);
                }
                catch
                {
                    // ignored
                }
            }

            // Remove the list of checkbox-pages. 
            CheckboxIds.Clear();
            Context.Items.Remove("CheckboxIds");
        }

        if (FixedWindowRateLimiter != null)
        {
            await FixedWindowRateLimiter.DisposeAsync();
            Context.Items.Remove("FixedWindowRateLimiter");
        }
    }

    public async Task<string?> SetCheckbox(string base64Id, int index, byte value)
    {
        var acquired = false;
        if (FixedWindowRateLimiter != null)
        {
            acquired = (await FixedWindowRateLimiter.AcquireAsync()).IsAcquired;
        }

        if (!acquired)
        {
            return "Too many requests. Try again later.";
        }

        if (base64Id.TryParse256BitBase64Id(out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(base64Id));
        }

        try
        {
            // Set state of checkbox.
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
            var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(parsedId);
            await checkboxGrain.SetCheckbox(index, value, userId);
        }

        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    #endregion
}
