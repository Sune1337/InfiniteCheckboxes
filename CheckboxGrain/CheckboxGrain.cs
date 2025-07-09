namespace CheckboxGrain;

using global::CheckboxGrain.Models;
using global::CheckboxGrain.Utils;

using GrainInterfaces;

using RedisMessages;

public class CheckboxGrain : Grain, ICheckboxGrain
{
    #region Constants

    private const int CheckboxPageSize = 4096;

    #endregion

    #region Fields

    private readonly IPersistentState<CheckboxState> _checkboxState;
    private readonly IRedisMessagePublisherManager _redisMessagePublisherManager;

    private bool[]? _decompressedData;
    private string? _grainId;

    #endregion

    #region Constructors and Destructors

    public CheckboxGrain(
        [PersistentState("CheckboxState", "CheckboxStore")]
        IPersistentState<CheckboxState> checkboxState,
        IRedisMessagePublisherManager redisMessagePublisherManager
    )
    {
        _checkboxState = checkboxState;
        _redisMessagePublisherManager = redisMessagePublisherManager;
    }

    #endregion

    #region Public Methods and Operators

    public Task<byte[]?> GetCheckboxes()
    {
        return Task.FromResult(_checkboxState.State?.Checkboxes);
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainId = this.GetPrimaryKeyString();
        return Task.CompletedTask;
    }

    public async Task SetCheckbox(int index, byte value)
    {
        var normalValue = value != 0;
        if (_checkboxState.State == null && !normalValue)
        {
            // There is no state, and user wants to set a value to 0.
            // The default value is 0 so no need to update state.
            return;
        }

        _checkboxState.State ??= new CheckboxState();
        _decompressedData ??= CompressedBitArray.Decompress(_checkboxState.State.Checkboxes) ?? new bool[CheckboxPageSize];

        if (index < 0 || index >= _decompressedData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_decompressedData[index] != normalValue)
        {
            // The checkbox state changed.
            _decompressedData[index] = normalValue;
            _checkboxState.State.Checkboxes = CompressedBitArray.Compress(_decompressedData);
            await _checkboxState.WriteStateAsync();
        }

        if (_grainId != null)
        {
            await _redisMessagePublisherManager.PublishCheckboxUpdateAsync(_grainId, index, normalValue);
        }
    }

    #endregion
}
