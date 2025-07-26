namespace GrainInterfaces.Minesweeper.Models;

[GenerateSerializer]
public class Minesweeper
{
    #region Do not reorder

    [Id(0)]
    public required string Id { get; set; }

    [Id(1)]
    public required uint Width { get; set; }

    [Id(2)]
    public required string FlagLocationId { get; set; }

    [Id(3)]
    public required string SweepLocationId { get; set; }

    [Id(4)]
    public required DateTime CreatedUtc { get; set; }

    [Id(5)]
    public DateTime? StartUtc { get; set; }

    [Id(6)]
    public DateTime? EndUtc { get; set; }

    [Id(7)]
    public uint[]? Mines { get; set; }

    [Id(8)]
    public Dictionary<int, int>? MineCounts { get; set; }

    [Id(9)]
    public ulong? Score { get; set; }

    #endregion
}
