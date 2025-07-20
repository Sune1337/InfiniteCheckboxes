namespace GrainInterfaces.GoldDigger;

public interface IGoldDiggerGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task<int[]> GetFoundGoldSpots();
    public Task IndexChecked(int index, string userId);

    #endregion
}
