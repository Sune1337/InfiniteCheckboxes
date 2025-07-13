namespace GrainInterfaces.War;

using GrainInterfaces.War.Models;

public interface IWarGameGrain : IGrainWithIntegerKey
{
    #region Public Methods and Operators

    public Task<War> CreateWar(int battleFieldWidth);
    public Task<War> GetWarState();

    #endregion
}
