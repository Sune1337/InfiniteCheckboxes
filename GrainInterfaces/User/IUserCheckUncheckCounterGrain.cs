namespace GrainInterfaces.User;

public interface IUserCheckUncheckCounterGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task AddCheckUncheck(int countChecked, int countUnchecked);

    #endregion
}
