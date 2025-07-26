namespace WarGrain.WarGame;

using System.Security.Cryptography;

using GrainInterfaces;
using GrainInterfaces.War;
using GrainInterfaces.War.Models;

using RedisMessages.WarUpdate;

using Two56bitId;

using WarGrain.WarGame.Models;

public class WarGameGrain : Grain, IWarGameGrain, ICheckboxCallbackGrain
{
    #region Fields

    private readonly IRedisWarUpdatePublisherManager _redisWarUpdatePublisherManager;
    private readonly IPersistentState<WarGameState> _warGameState;
    private long _grainId;

    #endregion

    #region Constructors and Destructors

    public WarGameGrain(
        [PersistentState("WarGameState", "WarStore")]
        IPersistentState<WarGameState> warGameState,
        IRedisWarUpdatePublisherManager redisWarUpdatePublisherManager
    )
    {
        _warGameState = warGameState;
        _redisWarUpdatePublisherManager = redisWarUpdatePublisherManager;
    }

    #endregion

    #region Public Methods and Operators

    public async Task<War> CreateWar(int battleFieldWidth)
    {
        _warGameState.State.WarLocationId = Random256Bit.GenerateHex();
        _warGameState.State.CreatedUtc = DateTime.UtcNow;
        _warGameState.State.BattlefieldWidth = battleFieldWidth;

        // Randomize the battlefield.
        var randomBytes = CreateRandomBattlefield(battleFieldWidth * battleFieldWidth);

        // Get the checkbox-grain and register this war-game for callbacks.
        var checkboxGrain = GrainFactory.GetGrain<ICheckboxGrain>(_warGameState.State.WarLocationId);
        await checkboxGrain.RegisterCallback<WarGameGrain>(this.GetGrainId());
        await checkboxGrain.SetCheckboxes(randomBytes);

        await WriteStateAndPublishAsync();
        return WarStateToWar();
    }

    public Task<War> GetWarState()
    {
        return Task.FromResult(WarStateToWar());
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainId = this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public async Task<Dictionary<int, bool>?> WhenCheckboxesUpdated(string id, bool[] checkboxes, int index, bool value, string userId)
    {
        if (_warGameState.State.EndUtc != null)
        {
            throw new Exception("War has ended!");
        }

        var battlefieldSize = _warGameState.State.BattlefieldWidth * _warGameState.State.BattlefieldWidth;
        if (index < 0 || index >= battlefieldSize)
        {
            throw new Exception("Stick to the battlefield!");
        }

        // Register when war started.
        _warGameState.State.StartUtc ??= DateTime.UtcNow;

        var countChecked = 0;
        var countUnchecked = 0;
        var gameSize = Math.Min(_warGameState.State.BattlefieldWidth * _warGameState.State.BattlefieldWidth, checkboxes.Length);
        for (var i = 0; i < gameSize; i++)
        {
            if (i == index ? value : checkboxes[i])
            {
                countChecked++;
            }
            else
            {
                countUnchecked++;
            }
        }

        _warGameState.State.NumberOfChecked = countChecked;
        _warGameState.State.NumberOfUnchecked = countUnchecked;

        if (countChecked >= gameSize)
        {
            // All checkboxes are checked.
            _warGameState.State.EndUtc = DateTime.UtcNow;
            _warGameState.State.WinningTeam = Team.Checkers;
        }
        else if (countUnchecked >= gameSize)
        {
            // All checkboxes are unchecked.
            _warGameState.State.EndUtc = DateTime.UtcNow;
            _warGameState.State.WinningTeam = Team.Uncheckers;
        }

        await WriteStateAndPublishAsync();
        return null;
    }

    #endregion

    #region Methods

    private byte[] CreateRandomBattlefield(int gameSize)
    {
        // Randomize the battlefield.
        var bytes = new byte[(gameSize + 7) / 8];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Count set bits
        var setBits = 0;
        for (var i = 0; i < gameSize; i++)
        {
            if ((bytes[i / 8] & (1 << (i % 8))) != 0)
                setBits++;
        }

        // Adjust to 50/50 distribution
        var target = gameSize / 2;
        while (setBits != target)
        {
            var i = Random.Shared.Next(gameSize);
            var byteIndex = i / 8;
            var bitMask = 1 << (i % 8);
            var isBitSet = (bytes[byteIndex] & bitMask) != 0;

            if ((setBits > target && isBitSet) || (setBits < target && !isBitSet))
            {
                bytes[byteIndex] ^= (byte)bitMask; // Flip the bit
                setBits += isBitSet ? -1 : 1;
            }
        }

        // Update the state
        _warGameState.State.NumberOfChecked = setBits;
        _warGameState.State.NumberOfUnchecked = gameSize - setBits;

        return bytes;
    }

    private War WarStateToWar()
    {
        return new War
        {
            Id = _grainId,
            WarLocationId = _warGameState.State.WarLocationId,
            CreatedUtc = _warGameState.State.CreatedUtc,
            StartUtc = _warGameState.State.StartUtc,
            EndUtc = _warGameState.State.EndUtc,
            BattlefieldWidth = _warGameState.State.BattlefieldWidth,
            NumberOfChecked = _warGameState.State.NumberOfChecked,
            NumberOfUnchecked = _warGameState.State.NumberOfUnchecked,
            WinningTeam = _warGameState.State.WinningTeam
        };
    }

    private async Task WriteStateAndPublishAsync()
    {
        await _warGameState.WriteStateAsync();
        await _redisWarUpdatePublisherManager.PublishWarUpdateAsync(_grainId, WarStateToWar());
    }

    #endregion
}
