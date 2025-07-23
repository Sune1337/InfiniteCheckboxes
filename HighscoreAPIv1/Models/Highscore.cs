namespace HighscoreAPIv1.Models;

public class Highscore
{
    #region Public Properties

    public required string Name { get; set; }

    public required IEnumerable<UserScore> Scores { get; set; }

    #endregion
}
