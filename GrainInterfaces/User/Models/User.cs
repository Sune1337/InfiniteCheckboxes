namespace GrainInterfaces.User.Models;

[GenerateSerializer]
public class User
{
    #region Do not reorder

    [Id(0)]
    public string? UserName { get; set; }

    [Id(1)]
    public ulong GoldBalance { get; set; }

    #endregion
}
