namespace WarGrain.Manager;

using GrainInterfaces.War;
using GrainInterfaces.War.Models;

using WarGrain.Manager.Models;

public class WarManagerGrain : Grain, IWarManagerGrain
{
    #region Fields

    private readonly IPersistentState<WarManagerState> _warManagerState;

    #endregion

    #region Constructors and Destructors

    public WarManagerGrain(
        [PersistentState("WarManagerState", "WarStore")]
        IPersistentState<WarManagerState> warManagerState
    )
    {
        _warManagerState = warManagerState;
    }

    #endregion

    #region Public Methods and Operators

    public async Task<War> GetCurrentWar()
    {
        var warGameGrain = GrainFactory.GetGrain<IWarGameGrain>(_warManagerState.State.CurrentWar);
        var warState = await warGameGrain.GetWarState();
        if (warState.CreatedUtc == null || warState.EndUtc != null)
        {
            // Start a new war.
            _warManagerState.State.CurrentWar++;
            warGameGrain = GrainFactory.GetGrain<IWarGameGrain>(_warManagerState.State.CurrentWar);
            warState = await warGameGrain.CreateWar(8);
            await _warManagerState.WriteStateAsync();
        }

        return warState;
    }

    public async Task<War> GetWar(long id)
    {
        if (id > _warManagerState.State.CurrentWar)
        {
            throw new Exception("War has not occured yet.");
        }

        // Get current war-game.
        var warGameGrain = GrainFactory.GetGrain<IWarGameGrain>(id);
        return await warGameGrain.GetWarState();
    }

    #endregion
}
