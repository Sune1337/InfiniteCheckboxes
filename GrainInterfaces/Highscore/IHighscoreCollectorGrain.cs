namespace GrainInterfaces.Highscore;

public interface IHighscoreCollectorGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task UpdateScore(string userId, ulong score);

    #endregion
}
