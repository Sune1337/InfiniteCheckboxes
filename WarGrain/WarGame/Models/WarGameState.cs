namespace WarGrain.WarGame.Models;

using GrainInterfaces.War.Models;

public class WarGameState
{
    #region Public Properties

    public int BattlefieldWidth { get; set; }
    public DateTime? CreatedUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public int NumberOfChecked { get; set; }
    public int NumberOfUnchecked { get; set; }
    public DateTime? StartUtc { get; set; }
    public string? WarLocationId { get; set; }
    public Team? WinningTeam { get; set; }

    #endregion
}
