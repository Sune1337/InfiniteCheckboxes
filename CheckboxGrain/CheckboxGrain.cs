namespace CheckboxGrain;

using global::CheckboxGrain.Models;
using global::CheckboxGrain.Utils;

using GrainInterfaces;
using GrainInterfaces.Statistics;

using RedisMessages;

public class CheckboxGrain : Grain, ICheckboxGrain
{
    #region Constants

    private const int CheckboxPageSize = 4096;

    #endregion

    #region Fields

    private readonly IPersistentState<CheckboxState> _checkboxState;
    private readonly IGrainFactory _grainFactory;
    private readonly IRedisCheckboxUpdatePublisherManager _redisCheckboxUpdatePublisherManager;

    private bool[]? _decompressedData;
    private string? _grainId;

    #endregion

    #region Constructors and Destructors

    public CheckboxGrain(
        [PersistentState("CheckboxState", "CheckboxStore")]
        IPersistentState<CheckboxState> checkboxState,
        IRedisCheckboxUpdatePublisherManager redisCheckboxUpdatePublisherManager,
        IGrainFactory grainFactory
    )
    {
        _checkboxState = checkboxState;
        _redisCheckboxUpdatePublisherManager = redisCheckboxUpdatePublisherManager;
        _grainFactory = grainFactory;
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

    public async Task RegisterCallback<T>(GrainId grainId) where T : ICheckboxCallbackGrain
    {
        _checkboxState.State.CallbackGrain = grainId;
        await _checkboxState.WriteStateAsync();
    }

    public async Task SetCheckbox(int index, byte value, string userId)
    {
        _decompressedData ??= CompressedBitArray.Decompress(_checkboxState.State.Checkboxes) ?? new bool[CheckboxPageSize];
        if (index < 0 || index >= _decompressedData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var normalValue = value != 0;
        if (_decompressedData[index] != normalValue)
        {
            // The checkbox state changed.
            if (_checkboxState.State.CallbackGrain != null)
            {
                // Invoke callback.
                var checkboxCallbackGrain = GrainFactory.GetGrain<ICheckboxCallbackGrain>(_checkboxState.State.CallbackGrain.Value);
                await checkboxCallbackGrain.WhenCheckboxesUpdated(_decompressedData, index, normalValue);
            }

            _decompressedData[index] = normalValue;
            _checkboxState.State.Checkboxes = CompressedBitArray.Compress(_decompressedData);
            await _checkboxState.WriteStateAsync();

            // Update global statistics.
            var checkUncheckCounter = _grainFactory.GetGrain<ICheckUncheckCounter>(0);
            await checkUncheckCounter.AddCheckUncheck(normalValue ? 1 : 0, normalValue ? 0 : 1);
        }

        if (_grainId != null)
        {
            await _redisCheckboxUpdatePublisherManager.PublishCheckboxUpdateAsync(_grainId, index, normalValue);
        }
    }

    public async Task SetCheckboxes(byte[] checkboxes)
    {
        _decompressedData ??= CompressedBitArray.Decompress(_checkboxState.State.Checkboxes) ?? new bool[CheckboxPageSize];

        var numberOfBits = Math.Min(checkboxes.Length * 8, _decompressedData.Length);
        for (var i = 0; i < numberOfBits; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            _decompressedData[i] = (checkboxes[byteIndex] & (1 << bitIndex)) != 0;
        }

        _checkboxState.State.Checkboxes = CompressedBitArray.Compress(_decompressedData);
        await _checkboxState.WriteStateAsync();
    }

    #endregion
}
