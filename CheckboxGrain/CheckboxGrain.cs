namespace CheckboxGrain;

using BitCoding;

using global::CheckboxGrain.Models;

using GrainInterfaces;
using GrainInterfaces.GoldDigger;
using GrainInterfaces.User;

using RedisMessages.CheckboxUpdate;

public class CheckboxGrain : Grain, ICheckboxGrain
{
    #region Constants

    private const int CheckboxPageSize = 4096;

    #endregion

    #region Fields

    private readonly IPersistentState<CheckboxState> _checkboxState;
    private readonly IRedisCheckboxUpdatePublisherManager _redisCheckboxUpdatePublisherManager;

    private bool[]? _decompressedData;
    private string _grainId = null!;

    #endregion

    #region Constructors and Destructors

    public CheckboxGrain(
        [PersistentState("CheckboxState", "CheckboxStore")]
        IPersistentState<CheckboxState> checkboxState,
        IRedisCheckboxUpdatePublisherManager redisCheckboxUpdatePublisherManager
    )
    {
        _checkboxState = checkboxState;
        _redisCheckboxUpdatePublisherManager = redisCheckboxUpdatePublisherManager;
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
        _decompressedData ??= BitArrayCoder.Decompress(_checkboxState.State.Checkboxes) ?? new bool[CheckboxPageSize];
        if (index < 0 || index >= _decompressedData.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var normalValue = value != 0;
        if (_decompressedData[index] != normalValue)
        {
            // The checkbox state changed.
            var skipUpdateStatistics = false;
            Dictionary<int, bool>? checkedFromCallback = null;
            if (_checkboxState.State.CallbackGrain != null)
            {
                skipUpdateStatistics = true;

                // Invoke callback.
                var checkboxCallbackGrain = GrainFactory.GetGrain<ICheckboxCallbackGrain>(_checkboxState.State.CallbackGrain.Value);
                checkedFromCallback = await checkboxCallbackGrain.WhenCheckboxesUpdated(_grainId, _decompressedData, index, normalValue, userId);
                if (checkedFromCallback != null)
                {
                    foreach (var (i, v) in checkedFromCallback)
                    {
                        _decompressedData[i] = v;
                    }
                }
            }
            else if (normalValue)
            {
                // Update gold-digger game.
                var goldDiggerGrain = GrainFactory.GetGrain<IGoldDiggerGrain>(_grainId);
                await goldDiggerGrain.IndexChecked(index, userId);
            }

            _decompressedData[index] = normalValue;
            _checkboxState.State.Checkboxes = BitArrayCoder.Compress(_decompressedData);
            await _checkboxState.WriteStateAsync();

            if (!skipUpdateStatistics)
            {
                // Update statistics.
                var checkUncheckCounter = GrainFactory.GetGrain<IUserCheckUncheckCounterGrain>(userId);
                await checkUncheckCounter.AddCheckUncheck((normalValue ? 1 : 0), (normalValue ? 0 : 1));
            }

            await _redisCheckboxUpdatePublisherManager.PublishCheckboxUpdateAsync(_grainId, index, normalValue);
            if (checkedFromCallback != null)
            {
                foreach (var (i, v) in checkedFromCallback)
                {
                    await _redisCheckboxUpdatePublisherManager.PublishCheckboxUpdateAsync(_grainId, i, v);
                }
            }
        }
    }

    public async Task SetCheckboxes(byte[] checkboxes)
    {
        _decompressedData ??= BitArrayCoder.Decompress(_checkboxState.State.Checkboxes) ?? new bool[CheckboxPageSize];

        var numberOfBits = Math.Min(checkboxes.Length * 8, _decompressedData.Length);
        for (var i = 0; i < numberOfBits; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            _decompressedData[i] = (checkboxes[byteIndex] & (1 << bitIndex)) != 0;
        }

        _checkboxState.State.Checkboxes = BitArrayCoder.Compress(_decompressedData);
        await _checkboxState.WriteStateAsync();
    }

    #endregion
}
