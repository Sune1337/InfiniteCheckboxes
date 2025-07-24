namespace UserGrain;

using global::UserGrain.Models;

using GrainInterfaces.Highscore;
using GrainInterfaces.Statistics;
using GrainInterfaces.User;
using GrainInterfaces.User.Models;

using RedisMessages.UserUpdate;

public class UserGrain : Grain, IUserGrain
{
    #region Fields

    private readonly IRedisUserUpdatePublisherManager _redisUserUpdatePublisherManager;
    private readonly IPersistentState<UserState> _userState;

    private string _grainId = null!;

    #endregion

    #region Constructors and Destructors

    public UserGrain(
        [PersistentState("UserState", "UserStore")]
        IPersistentState<UserState> userState,
        IRedisUserUpdatePublisherManager redisUserUpdatePublisherManager
    )
    {
        _userState = userState;
        _redisUserUpdatePublisherManager = redisUserUpdatePublisherManager;
    }

    #endregion

    #region Public Methods and Operators

    public async Task AddCheckUncheck(int countChecked, int countUnchecked)
    {
        _userState.State.CountChecked += (ulong)countChecked;
        _userState.State.CountUnchecked += (ulong)countUnchecked;
        await _userState.WriteStateAsync();

        var checkedHighscoreGrain = GrainFactory.GetGrain<IHighscoreCollectorGrain>(HighscoreLists.Checked);
        var uncheckedHighscoreGrain = GrainFactory.GetGrain<IHighscoreCollectorGrain>(HighscoreLists.Unchecked);
        var checkUncheckCounter = GrainFactory.GetGrain<ICheckUncheckCounter>(0);

        await Task.WhenAll(
            // Update user highscore.
            checkedHighscoreGrain.UpdateScore(_grainId, _userState.State.CountChecked),
            uncheckedHighscoreGrain.UpdateScore(_grainId, _userState.State.CountUnchecked),

            // Update global statistics.
            checkUncheckCounter.AddCheckUncheck(countChecked, countUnchecked)
        );
    }

    public async Task AddGold(int amount)
    {
        _userState.State.GoldBalance += (ulong)amount;
        await WriteStateAndPublishAsync();

        var goldDiggerHighscoreGrain = GrainFactory.GetGrain<IHighscoreCollectorGrain>(HighscoreLists.GoldDigger);
        await goldDiggerHighscoreGrain.UpdateScore(_grainId, _userState.State.GoldBalance);
    }

    public Task<User> GetUser()
    {
        return Task.FromResult(UserStateToUser());
    }

    public Task<string?> GetUserName()
    {
        return Task.FromResult(_userState.State.UserName);
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainId = this.GetPrimaryKeyString();
        return Task.CompletedTask;
    }

    public async Task SetUserName(string? userName)
    {
        _userState.State.UserName = userName;
        await _userState.WriteStateAsync();
    }

    #endregion

    #region Methods

    private User UserStateToUser()
    {
        return new User
        {
            UserName = _userState.State.UserName,
            GoldBalance = _userState.State.GoldBalance
        };
    }

    private async Task WriteStateAndPublishAsync()
    {
        await _userState.WriteStateAsync();
        await _redisUserUpdatePublisherManager.PublishUserUpdateAsync(_grainId, UserStateToUser());
    }

    #endregion
}
