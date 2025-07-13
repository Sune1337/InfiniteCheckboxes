namespace GrainInterfaces.War;

using GrainInterfaces.War.Models;

public interface IWarManagerGrain : IGrainWithIntegerKey
{
    #region Public Methods and Operators

    public Task<War> GetCurrentWar();
    public Task<War> GetWar(long id);

    #endregion
}
