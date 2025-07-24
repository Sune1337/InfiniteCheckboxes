namespace UserAPI.Controllers;

using System.Security.Claims;

using global::UserAPI.Models;

using GrainInterfaces.User;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v1/[controller]/[action]")]
public class UserAPI : ControllerBase
{
    #region Fields

    private readonly IGrainFactory _grainFactory;

    #endregion

    #region Constructors and Destructors

    public UserAPI(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    #endregion

    #region Public Methods and Operators

    [HttpGet]
    public async Task<UserDetails> GetUserDetails()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
        var userGrain = _grainFactory.GetGrain<IUserGrain>(userId);
        var user = await userGrain.GetUser();

        return new UserDetails
        {
            UserName = user.UserName
        };
    }

    [HttpPut]
    public async Task SetUserDetails(UserDetails userDetails)
    {
        if (userDetails.UserName?.Length > 25)
        {
            throw new ArgumentException("User name too long.");
        }
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User not logged in.");
        var userGrain = _grainFactory.GetGrain<IUserGrain>(userId);
        await userGrain.SetUserName(userDetails.UserName);
    }

    #endregion
}
