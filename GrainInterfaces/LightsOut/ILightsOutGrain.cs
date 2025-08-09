namespace GrainInterfaces.LightsOut;

using GrainInterfaces.LightsOut.Models;

public interface ILightsOutGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task CreateGame(uint width, string userId);
    public Task<LightsOut> GetLightsOut();

    #endregion
}
