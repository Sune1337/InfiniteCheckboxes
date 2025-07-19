namespace GrainInterfaces.Statistics;

public interface ICheckUncheckCounter : IGrainWithIntegerKey
{
    #region Public Methods and Operators

    public Task AddCheckUncheck(int countChecked, int countUnchecked);

    #endregion
}
