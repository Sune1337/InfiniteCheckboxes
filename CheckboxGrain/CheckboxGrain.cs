namespace CheckboxGrain;

using global::CheckboxGrain.Models;

using GrainInterfaces;

public class CheckboxGrain : Grain, ICheckboxGrain
{
    #region Constants

    private const int CheckboxPageSize = 1000;

    #endregion

    #region Static Fields

    private static readonly byte[] EmptyCheckboxPage = new byte[CheckboxPageSize];

    #endregion

    #region Fields

    private readonly IPersistentState<CheckboxState> _checkboxState;

    #endregion

    #region Constructors and Destructors

    public CheckboxGrain(
        [PersistentState("CheckboxState", "CheckboxStore")]
        IPersistentState<CheckboxState> checkboxState
    )
    {
        _checkboxState = checkboxState;
    }

    #endregion

    #region Public Methods and Operators

    public Task<byte[]> GetCheckboxes()
    {
        return Task.FromResult(_checkboxState.State?.Checkboxes ?? EmptyCheckboxPage);
    }

    public async Task SetCheckbox(int index, byte value)
    {
        var normalValue = value == 0 ? (byte)0 : (byte)1;

        if (_checkboxState.State == null && normalValue == 0)
        {
            // There is no state, and user wants to set a value to 0.
            // The default value is 0 so no need to update state.
            return;
        }

        _checkboxState.State ??= new CheckboxState();
        _checkboxState.State.Checkboxes ??= new byte[CheckboxPageSize];

        if (index < 0 || index >= _checkboxState.State.Checkboxes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_checkboxState.State.Checkboxes[index] != normalValue)
        {
            // The checkbox state changed.
            _checkboxState.State.Checkboxes[index] = normalValue;
            await _checkboxState.WriteStateAsync();
        }
    }

    #endregion
}
