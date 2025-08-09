namespace LightsOutGrain;

using global::LightsOutGrain.Models;

using GrainInterfaces.Checkbox;
using GrainInterfaces.LightsOut;
using GrainInterfaces.LightsOut.Models;

using RedisMessages.LightsOutUpdate;

using Two56bitId;

public class LightsOutGrain : Grain, ILightsOutGrain, ICheckboxCallbackGrain
{
    #region Fields

    private readonly IPersistentState<LightsOutState> _lightsOutState;
    private readonly IRedisLightsOutUpdatePublisherManager _redisLightsOutUpdatePublisherManager;
    private string _grainId = null!;

    #endregion

    #region Constructors and Destructors

    public LightsOutGrain(
        [PersistentState("LightsOutState", "LightsOutStore")]
        IPersistentState<LightsOutState> lightsOutState,
        IRedisLightsOutUpdatePublisherManager redisLightsOutUpdatePublisherManager
    )
    {
        _lightsOutState = lightsOutState;
        _redisLightsOutUpdatePublisherManager = redisLightsOutUpdatePublisherManager;
    }

    #endregion

    #region Public Methods and Operators

    public async Task CreateGame(uint width, string userId)
    {
        if (width < 3 || width > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var gameSize = width * width;

        _lightsOutState.State.CreatedUtc = DateTime.UtcNow;
        _lightsOutState.State.Width = width;
        _lightsOutState.State.UserId = userId;
        _lightsOutState.State.LightsLocationId = Random256Bit.GenerateHex();

        // Get the checkbox-grains and register this game for callbacks.
        var checkboxGrain = GrainFactory.GetGrain<ICheckboxGrain>(_lightsOutState.State.LightsLocationId);
        await checkboxGrain.RegisterCallback<LightsOutGrain>(this.GetGrainId());

        // Scramble board.
        var checkboxes = new byte[(gameSize + 7) / 8];
        for (var i = 0; i < gameSize / 2; i++)
        {
            var randomIndex = Random.Shared.Next((int)gameSize);
            var byteIndex = randomIndex / 8;
            var bitIndex = randomIndex % 8;
            checkboxes[byteIndex] ^= (byte)(1 << bitIndex);

            foreach (var index in IterateSurroundingIndexes(randomIndex, (int)width))
            {
                byteIndex = index / 8;
                bitIndex = index % 8;
                checkboxes[byteIndex] ^= (byte)(1 << bitIndex);
            }
        }

        await checkboxGrain.SetCheckboxes(checkboxes);

        await _lightsOutState.WriteStateAsync();
    }

    public Task<LightsOut> GetLightsOut()
    {
        return Task.FromResult(LightsOutStateToLightsOut());
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainId = this.GetPrimaryKeyString();
        return Task.CompletedTask;
    }

    public async Task<Dictionary<int, bool>?> WhenCheckboxesUpdated(string id, bool[] checkboxes, int index, bool value, string userId)
    {
        if (_lightsOutState.State.EndUtc != null)
        {
            throw new Exception("The game has ended!");
        }

        if (_lightsOutState.State.Width == null)
        {
            throw new Exception("Width is null.");
        }

        var width = _lightsOutState.State.Width.Value;
        var gameSize = width * width;
        if (index < 0 || index >= gameSize)
        {
            throw new Exception("Stick to the game-area!");
        }

        if (userId != _lightsOutState.State.UserId)
        {
            throw new Exception("Not your game!");
        }

        // Register when war started.
        var saveAndPublishState = false;
        if (_lightsOutState.State.StartUtc == null)
        {
            saveAndPublishState = true;
            _lightsOutState.State.StartUtc = DateTime.UtcNow;
        }

        var result = new Dictionary<int, bool>();
        checkboxes[index] = !checkboxes[index];
        foreach (var i in IterateSurroundingIndexes(index, (int)_lightsOutState.State.Width.Value))
        {
            checkboxes[i] = !checkboxes[i];
            result.Add(i, checkboxes[i]);
        }

        // Count checks to see is game is done.
        var countChecked = 0;
        for (var i = 0; i < gameSize; i++)
        {
            if (checkboxes[i])
            {
                countChecked++;
            }
        }

        if (countChecked == 0)
        {
            // User finished!
            saveAndPublishState = true;
            _lightsOutState.State.EndUtc = DateTime.UtcNow;
        }

        if (saveAndPublishState)
        {
            // Save state and publish.
            await _lightsOutState.WriteStateAsync();
            await _redisLightsOutUpdatePublisherManager.PublishLightsOutUpdateAsync(_grainId, LightsOutStateToLightsOut());
        }

        return result;
    }

    #endregion

    #region Methods

    private IEnumerable<int> IterateSurroundingIndexes(int index, int stride)
    {
        var exclusiveMaxIndex = stride * stride;

        // Up
        if (index >= stride)
        {
            yield return index - stride;
        }

        // Right
        if ((index + 1) % stride > 0)
        {
            yield return index + 1;
        }

        // Down
        if (index + stride < exclusiveMaxIndex)
        {
            yield return index + stride;
        }

        // Left
        if ((index % stride) > 0)
        {
            yield return index - 1;
        }
    }

    private LightsOut LightsOutStateToLightsOut()
    {
        return new LightsOut
        {
            CreatedUtc = _lightsOutState.State.CreatedUtc,
            EndUtc = _lightsOutState.State.EndUtc,
            LightsLocationId = _lightsOutState.State.LightsLocationId,
            StartUtc = _lightsOutState.State.StartUtc,
            UserId = _lightsOutState.State.UserId,
            Width = _lightsOutState.State.Width
        };
    }

    #endregion
}
