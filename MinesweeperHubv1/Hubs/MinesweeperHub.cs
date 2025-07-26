namespace MinesweeperHubv1.Hubs;

using System.Security.Claims;

using GrainInterfaces.Minesweeper;
using GrainInterfaces.Minesweeper.Models;

using Microsoft.AspNetCore.SignalR;

using MinesweeperHubv1.MinesweeperObserver;

using Two56bitId;

public class MinesweeperHub : Hub
{
    #region Fields

    private readonly IGrainFactory _grainFactory;
    private readonly IMinesweeperObserverManager _minesweeperObserverManager;

    #endregion

    #region Constructors and Destructors

    public MinesweeperHub(IGrainFactory grainFactory, IMinesweeperObserverManager minesweeperObserverManager)
    {
        _grainFactory = grainFactory;
        _minesweeperObserverManager = minesweeperObserverManager;
    }

    #endregion

    #region Properties

    private HashSet<string>? MinesweeperIds => Context.Items["MinesweeperIds"] as HashSet<string>;

    #endregion

    #region Public Methods and Operators

    public async Task<string> CreateGame(uint width, uint numberOfMines)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
        var minesweeperId = Random256Bit.GenerateHex();
        var minesweeperGrain = _grainFactory.GetGrain<IMinesweeperGrain>(minesweeperId);
        await minesweeperGrain.CreateGame(width, numberOfMines, userId);
        return minesweeperId;
    }


    public async Task<Minesweeper> MinesweeperSubscribe(byte[] byteId)
    {
        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();
        if (MinesweeperIds?.Contains(hexId) == false)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.MinesweeperGroupPrefix}_{hexId}");
            await _minesweeperObserverManager.SubscribeAsync(hexId);

            // Remember that the current client subscribes to this minesweeper.
            MinesweeperIds?.Add(hexId);
        }

        // Get the initial state of minesweeper.
        var minesweeperGrain = _grainFactory.GetGrain<IMinesweeperGrain>(hexId);
        return await minesweeperGrain.GetMinesweeper();
    }

    public async Task MinesweeperUnsubscribe(byte[] byteId)
    {
        if (byteId.Length > 32)
        {
            throw new ArgumentException("Id is too big.");
        }

        var hexId = Convert.ToHexStringLower(byteId).TrimLeadingZeroPairs();
        if (MinesweeperIds?.Contains(hexId) == false)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.MinesweeperGroupPrefix}_{hexId}");
        await _minesweeperObserverManager.UnsubscribeAsync(hexId);

        // The current connection no longer subscribes to the war.
        MinesweeperIds?.Remove(hexId);
    }

    public override Task OnConnectedAsync()
    {
        // Create a list to keep track of which minesweepers the connection subscribes to.
        Context.Items.Add("MinesweeperIds", new HashSet<string>());
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (MinesweeperIds != null)
        {
            foreach (var id in MinesweeperIds)
            {
                try
                {
                    await _minesweeperObserverManager.UnsubscribeAsync(id);
                }
                catch
                {
                    // ignored
                }
            }

            // Remove the list of minesweepers. 
            MinesweeperIds.Clear();
            Context.Items.Remove("MinesweeperIds");
        }
    }

    #endregion
}
