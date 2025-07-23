namespace GrainInterfaces.Highscore;

public interface IHighscoreGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task UpdateScore(Dictionary<string, ulong> scores);

    #endregion
}
