namespace HighscoreGrain.Models;

public class HighscoreState
{
    #region Public Properties

    public Dictionary<string, ulong> Highscores { get; set; } = new();

    #endregion
}
