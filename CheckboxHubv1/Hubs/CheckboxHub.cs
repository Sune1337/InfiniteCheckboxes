namespace CheckboxHubv1.Hubs;

using System.Security.Claims;
using System.Threading.RateLimiting;

using CheckboxHubv1.CheckboxObserver;
using CheckboxHubv1.Statistics;
using CheckboxHubv1.UserObserver;

using GrainInterfaces;
using GrainInterfaces.GoldDigger;
using GrainInterfaces.User;

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
    private readonly IUserObserverManager _userObserverManager;

    #endregion

    #region Constructors and Destructors

    public CheckboxHub(IGrainFactory grainFactory, ICheckboxObserverManager checkboxObserverManager, IStatisticsObserverManager statisticsObserverManager, IUserObserverManager userObserverManager)
    {
        _grainFactory = grainFactory;
        _checkboxObserverManager = checkboxObserverManager;
        _statisticsObserverManager = statisticsObserverManager;
        _userObserverManager = userObserverManager;
    }

    #endregion

    #region Properties

    private HashSet<string>? CheckboxIds => Context.Items["CheckboxIds"] as HashSet<string>;
    private FixedWindowRateLimiter? FixedWindowRateLimiter => Context.Items["FixedWindowRateLimiter"] as FixedWindowRateLimiter;

    #endregion

    #region Public Methods and Operators

    public async Task<byte[]?> CheckboxesSubscribe(byte[] byteId, bool subscribeToStatistics)
    {
        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();
        if (CheckboxIds?.Contains(hexId) == false)
        {
            if (subscribeToStatistics)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxStatisticsGroupPrefix}_{hexId}");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{hexId}");
            await _checkboxObserverManager.SubscribeAsync(hexId);

            // Remember that the current client subscribes to this checkbox-page.
            CheckboxIds?.Add(hexId);

            // Seed client with gold-spots.
            var goldDiggerGrain = _grainFactory.GetGrain<IGoldDiggerGrain>(hexId);
            var goldSpots = await goldDiggerGrain.GetFoundGoldSpots();
            if ((goldSpots?.Length ?? 0) > 0)
            {
                await Clients.Caller.SendAsync("GoldSpot", byteId, goldSpots);
            }
        }

        // Get the initial state of checkboxes.
        var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(hexId);
        return await checkboxGrain.GetCheckboxes();
    }

    public async Task CheckboxesUnsubscribe(byte[] byteId)
    {
        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();
        if (CheckboxIds?.Contains(hexId) == false)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{hexId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxStatisticsGroupPrefix}_{hexId}");
        await _checkboxObserverManager.UnsubscribeAsync(hexId);

        // The current connection no longer subscribes to the checkbox-page.
        CheckboxIds?.Remove(hexId);
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

        // Subscribe to user updates.
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
        await _userObserverManager.SubscribeAsync(userId);

        // Get the current user to seed client.
        var userGrain = _grainFactory.GetGrain<IUserGrain>(userId);
        var user = await userGrain.GetUser();
        await Clients.Caller.SendAsync("UB", new { user.GoldBalance });
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

        // Unsubscribe to user updates.
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
        await _userObserverManager.UnsubscribeAsync(userId);
    }

    public async Task<string?> SetCheckbox(byte[] byteId, int index, byte value)
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

        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();

        try
        {
            // Set state of checkbox.
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
            var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(hexId);
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
