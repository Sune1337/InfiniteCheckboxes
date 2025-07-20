namespace GrainInterfaces;

public interface ICheckboxGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task<byte[]?> GetCheckboxes();
    public Task RegisterCallback<T>(GrainId grainId) where T : ICheckboxCallbackGrain;
    public Task SetCheckbox(int index, byte value, string userId);
    public Task SetCheckboxes(byte[] checkboxes);

    #endregion
}
