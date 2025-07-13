namespace GrainInterfaces;

public interface ICheckboxCallbackGrain : IAddressable
{
    #region Public Methods and Operators

    public Task WhenCheckboxesUpdated(bool[] checkboxes, int index, bool value);

    #endregion
}
