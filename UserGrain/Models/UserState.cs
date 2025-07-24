namespace UserGrain.Models;

public class UserState
{
    #region Public Properties

    public ulong CountChecked { get; set; }
    public ulong CountUnchecked { get; set; }
    public ulong GoldBalance { get; set; }
    public string? UserName { get; set; }

    #endregion
}
