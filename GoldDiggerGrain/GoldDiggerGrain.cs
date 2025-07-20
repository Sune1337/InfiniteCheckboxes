namespace GoldDiggerGrain;

using global::GoldDiggerGrain.Models;

using GrainInterfaces.GoldDigger;
using GrainInterfaces.User;

using RandN.Rngs;

using RedisMessages.CheckboxUpdate;

using RngWithSecret;

public class GoldDiggerGrain : Grain, IGoldDiggerGrain
{
    #region Constants

    private const int NumberOfGoldSpots = 16;

    #endregion

    #region Fields

    private readonly IPersistentState<GoldDiggerState> _goldDiggerState;
    private readonly IRedisCheckboxUpdatePublisherManager _redisCheckboxUpdatePublisherManager;
    private readonly SecretPcg32Factory _secretPcg32Factory;

    private Pcg32? _goldDiggerRng;
    private string _grainId = null!;

    #endregion

    #region Constructors and Destructors

    public GoldDiggerGrain(
        [PersistentState("GoldDiggerState", "GoldDiggerStore")]
        IPersistentState<GoldDiggerState> goldDiggerState,
        SecretPcg32Factory secretPcg32Factory,
        IRedisCheckboxUpdatePublisherManager redisCheckboxUpdatePublisherManager
    )
    {
        _goldDiggerState = goldDiggerState;
        _secretPcg32Factory = secretPcg32Factory;
        _redisCheckboxUpdatePublisherManager = redisCheckboxUpdatePublisherManager;
    }

    #endregion

    #region Properties

    private Pcg32 GoldDiggerRng => _goldDiggerRng ??= _secretPcg32Factory.Create("GoldDiggerRng").GetRngForAddress(_grainId);

    #endregion

    #region Public Methods and Operators

    public Task<int[]> GetFoundGoldSpots()
    {
        return Task.FromResult(
            _goldDiggerState.State.GoldSpots
                .Where(x => x.Value)
                .Select(x => x.Key)
                .ToArray()
        );
    }

    public async Task IndexChecked(int index, string userId)
    {
        if (_goldDiggerState.State.GoldSpots.TryGetValue(index, out var goldSpotFound) && !goldSpotFound)
        {
            // Update state.
            _goldDiggerState.State.GoldSpots[index] = true;
            await _goldDiggerState.WriteStateAsync();

            // Add gold to the user.
            var userGrain = GrainFactory.GetGrain<IUserGrain>(userId);
            await userGrain.AddGold(1);

            // Publish the new gold spot.
            await _redisCheckboxUpdatePublisherManager.PublishGoldSpotAsync(_grainId, index);
        }
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainId = this.GetPrimaryKeyString();

        if (_goldDiggerState.State.GoldSpots.Count < NumberOfGoldSpots)
        {
            GenerateGoldSpots();
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Methods

    private void GenerateGoldSpots()
    {
        var goldSpots = _goldDiggerState.State.GoldSpots;
        while (goldSpots.Count < NumberOfGoldSpots)
        {
            var index = (int)GoldDiggerRng.NextUInt32() & 0xfff;
            goldSpots.TryAdd(index, false);
        }
    }

    #endregion
}
