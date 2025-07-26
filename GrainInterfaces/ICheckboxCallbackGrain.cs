namespace GrainInterfaces;

public interface ICheckboxCallbackGrain : IAddressable
{
    #region Public Methods and Operators

    public Task<Dictionary<int, bool>?> WhenCheckboxesUpdated(string id, bool[] checkboxes, int index, bool value, string userId);

    #endregion
}
