namespace GrainInterfaces.Statistics.Models;

[GenerateSerializer]
public class CheckboxCounters
{
    #region Do not reorder

    [Id(0)]
    public ulong NumberOfChecked { get; set; }

    [Id(1)]
    public ulong NumberOfUnchecked { get; set; }

    #endregion
}
