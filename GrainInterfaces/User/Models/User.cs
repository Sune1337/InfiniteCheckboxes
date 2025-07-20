namespace GrainInterfaces.User.Models;

[GenerateSerializer]
public class User
{
    #region Do not reorder

    [Id(0)]
    public int GoldBalance { get; set; }

    #endregion
}
