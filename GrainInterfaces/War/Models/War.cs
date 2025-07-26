namespace GrainInterfaces.War.Models;

[GenerateSerializer]
public class War
{
    #region Do not reorder

    [Id(0)]
    public required long Id { get; set; }

    [Id(1)]
    public string? WarLocationId { get; set; }

    [Id(2)]
    public DateTime? CreatedUtc { get; set; }

    [Id(3)]
    public DateTime? StartUtc { get; set; }

    [Id(4)]
    public DateTime? EndUtc { get; set; }

    [Id(5)]
    public int BattlefieldWidth { get; set; }

    [Id(6)]
    public int NumberOfChecked { get; set; }

    [Id(7)]
    public int NumberOfUnchecked { get; set; }

    [Id(8)]
    public Team? WinningTeam { get; set; }

    #endregion
}
