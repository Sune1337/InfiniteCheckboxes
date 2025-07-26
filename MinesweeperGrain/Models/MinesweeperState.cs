namespace MinesweeperGrain.Models;

public class MinesweeperState
{
    #region Public Properties

    public DateTime? CreatedUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public string? FlagLocationId { get; set; }
    public Dictionary<uint, bool> Mines { get; set; } = new();
    public DateTime? StartUtc { get; set; }
    public string? SweepLocationId { get; set; }
    public string? UserId { get; set; }
    public uint? Width { get; set; }
    public bool? Win { get; set; }

    #endregion
}
