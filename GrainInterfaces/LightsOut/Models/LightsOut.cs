namespace GrainInterfaces.LightsOut.Models;

[GenerateSerializer]
public class LightsOut
{
    #region Do not reorder

    [Id(0)]
    public DateTime? CreatedUtc { get; set; }

    [Id(1)]
    public DateTime? EndUtc { get; set; }

    [Id(2)]
    public string? LightsLocationId { get; set; }

    [Id(3)]
    public DateTime? StartUtc { get; set; }

    [Id(4)]
    public string? UserId { get; set; }

    [Id(5)]
    public uint? Width { get; set; }

    #endregion
}
