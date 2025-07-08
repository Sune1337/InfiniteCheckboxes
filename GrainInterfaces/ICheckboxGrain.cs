namespace GrainInterfaces;

public interface ICheckboxGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task<byte[]> GetCheckboxes();
    public Task SetCheckbox(int index, byte value);

    #endregion
}
