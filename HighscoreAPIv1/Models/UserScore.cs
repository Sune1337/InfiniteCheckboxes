namespace HighscoreAPIv1.Models;

public class UserScore
{
    #region Public Properties

    public required ulong Score { get; set; }
    public required string Username { get; set; }

    #endregion
}
