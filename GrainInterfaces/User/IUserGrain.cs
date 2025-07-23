namespace GrainInterfaces.User;

using GrainInterfaces.User.Models;

public interface IUserGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task AddCheckUncheck(int countChecked, int countUnchecked);
    public Task AddGold(int amount);
    public Task<User> GetUser();
    public Task<string?> GetUserName();
    public Task SetUserName(string? userName);

    #endregion
}
