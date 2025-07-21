namespace WarHubv1.Hubs;

using GrainInterfaces.War;
using GrainInterfaces.War.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using WarHubv1.WarObserver;

[Authorize]
public class WarHub : Hub
{
    #region Fields

    private readonly IGrainFactory _grainFactory;
    private readonly IWarObserverManager _warObserverManager;

    #endregion

    #region Constructors and Destructors

    public WarHub(IGrainFactory grainFactory, IWarObserverManager warObserverManager)
    {
        _grainFactory = grainFactory;
        _warObserverManager = warObserverManager;
    }

    #endregion

    #region Properties

    private HashSet<long>? WarIds => Context.Items["WarIds"] as HashSet<long>;

    #endregion

    #region Public Methods and Operators

    public async Task<long> GetCurrentWarId()
    {
        var warManagerGrain = _grainFactory.GetGrain<IWarManagerGrain>(0);
        var currentWar = await warManagerGrain.GetCurrentWar();
        return currentWar.Id;
    }

    public override Task OnConnectedAsync()
    {
        // Create a list to keep track of which wars the connection subscribes to.
        Context.Items.Add("WarIds", new HashSet<long>());
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (WarIds != null)
        {
            foreach (var id in WarIds)
            {
                try
                {
                    await _warObserverManager.UnsubscribeAsync(id);
                }
                catch
                {
                    // ignored
                }
            }

            // Remove the list of wars. 
            WarIds.Clear();
            Context.Items.Remove("WarIds");
        }
    }


    public async Task<War> WarsSubscribe(long id)
    {
        if (WarIds?.Contains(id) == false)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.WarGroupPrefix}_{id}");
            await _warObserverManager.SubscribeAsync(id);

            // Remember that the current client subscribes to this war.
            WarIds?.Add(id);
        }

        // Get the initial state of war.
        var warManagerGrain = _grainFactory.GetGrain<IWarManagerGrain>(0);
        return await warManagerGrain.GetWar(id);
    }

    public async Task WarsUnsubscribe(long id)
    {
        if (WarIds?.Contains(id) == false)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.WarGroupPrefix}_{id}");
        await _warObserverManager.UnsubscribeAsync(id);

        // The current connection no longer subscribes to the war.
        WarIds?.Remove(id);
    }

    #endregion
}
