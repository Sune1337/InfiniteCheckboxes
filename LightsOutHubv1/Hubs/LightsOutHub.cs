namespace LightsOutHubv1.Hubs;

using System.Security.Claims;

using GrainInterfaces.LightsOut;
using GrainInterfaces.LightsOut.Models;

using LightsOutHubv1.LightsOutObserver;

using Microsoft.AspNetCore.SignalR;

using Two56bitId;

public class LightsOutHub : Hub
{
    #region Fields

    private readonly IGrainFactory _grainFactory;
    private readonly ILightsOutObserverManager _lightsOutObserverManager;

    #endregion

    #region Constructors and Destructors

    public LightsOutHub(IGrainFactory grainFactory, ILightsOutObserverManager lightsOutObserverManager)
    {
        _grainFactory = grainFactory;
        _lightsOutObserverManager = lightsOutObserverManager;
    }

    #endregion

    #region Properties

    private HashSet<string>? LightsOutIds => Context.Items["LightsOutIds"] as HashSet<string>;

    #endregion

    #region Public Methods and Operators

    public async Task<string> CreateGame(uint width)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
        var lightsOutId = Random256Bit.GenerateHex();
        var lightsOutGrain = _grainFactory.GetGrain<ILightsOutGrain>(lightsOutId);
        await lightsOutGrain.CreateGame(width, userId);
        return lightsOutId;
    }

    public async Task<LightsOut> LightsOutSubscribe(byte[] byteId)
    {
        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();
        if (LightsOutIds?.Contains(hexId) == false)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.LightsOutGroupPrefix}_{hexId}");
            await _lightsOutObserverManager.SubscribeAsync(hexId);

            // Remember that the current client subscribes to this lights-out.
            LightsOutIds?.Add(hexId);
        }

        // Get the initial state of lights-out.
        var lightsOutGrain = _grainFactory.GetGrain<ILightsOutGrain>(hexId);
        return await lightsOutGrain.GetLightsOut();
    }

    public async Task LightsOutUnsubscribe(byte[] byteId)
    {
        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();
        if (LightsOutIds?.Contains(hexId) == false)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.LightsOutGroupPrefix}_{hexId}");
        await _lightsOutObserverManager.UnsubscribeAsync(hexId);

        // The current connection no longer subscribes to the war.
        LightsOutIds?.Remove(hexId);
    }

    public override Task OnConnectedAsync()
    {
        // Create a list to keep track of which lights-out the connection subscribes to.
        Context.Items.Add("LightsOutIds", new HashSet<string>());
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (LightsOutIds != null)
        {
            foreach (var id in LightsOutIds)
            {
                try
                {
                    await _lightsOutObserverManager.UnsubscribeAsync(id);
                }
                catch
                {
                    // ignored
                }
            }

            // Remove the list of lights-outs. 
            LightsOutIds.Clear();
            Context.Items.Remove("LightsOutIds");
        }
    }

    #endregion
}
