namespace GrainInterfaces;

public interface ICheckboxGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task<string> Hello();

    #endregion
}
