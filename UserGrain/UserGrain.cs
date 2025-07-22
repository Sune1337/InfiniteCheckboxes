namespace UserGrain;

using global::UserGrain.Models;

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

    public async Task AddGold(int amount)
    {
        _userState.State.GoldBalance += amount;
        await WriteStateAndPublishAsync();
    }

    public Task<User> GetUser()
    {
        return Task.FromResult(UserStateToUser());
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
