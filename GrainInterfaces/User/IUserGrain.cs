namespace GrainInterfaces.User;

using GrainInterfaces.User.Models;

public interface IUserGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task AddGold(int amount);
    public Task<User> GetUser();

    #endregion
}
