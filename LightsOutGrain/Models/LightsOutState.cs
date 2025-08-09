namespace LightsOutGrain.Models;

public class LightsOutState
{
    #region Public Properties

    public DateTime? CreatedUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public string? LightsLocationId { get; set; }
    public DateTime? StartUtc { get; set; }
    public string? UserId { get; set; }
    public uint? Width { get; set; }

    #endregion
}
