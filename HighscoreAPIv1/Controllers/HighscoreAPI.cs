namespace HighscoreAPIv1.Controllers;

using System.Collections.Concurrent;

using GrainInterfaces.Highscore;
using GrainInterfaces.User;

using HighscoreAPIv1.Models;

using LazyCache;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v1/[controller]/[action]")]
public class HighscoreAPI : ControllerBase
{
    #region Fields

    private readonly IAppCache _cache;
    private readonly IGrainFactory _grainFactory;

    #endregion

    #region Constructors and Destructors

    public HighscoreAPI(IGrainFactory grainFactory, IAppCache cache)
    {
        _grainFactory = grainFactory;
        _cache = cache;
    }

    #endregion

    #region Public Methods and Operators

    [HttpGet]
    public async Task<Highscore> GetHighscores(string name)
    {
        return await _cache.GetOrAddAsync($"Highscores_{name}", () => LoadHighscoreData(name), DateTime.UtcNow.AddSeconds(10));
    }

    #endregion

    #region Methods

    private async Task<Highscore> LoadHighscoreData(string name)
    {
        var highscoreGrain = _grainFactory.GetGrain<IHighscoreGrain>(name);
        var highscores = await highscoreGrain.GetScores();

        var userIds = highscores
            .Select(d => d.Key)
            .Distinct();

        var usernames = new ConcurrentDictionary<string, string>();
        await Parallel.ForEachAsync(userIds, async (userId, token) =>
        {
            var userGrain = _grainFactory.GetGrain<IUserGrain>(userId);
            var username = await userGrain.GetUserName();
            if (username != null)
            {
                usernames.TryAdd(userId, username);
            }
        });

        return new Highscore
        {
            Name = name,
            Scores = highscores.Select(kv => new UserScore { Username = usernames.TryGetValue(kv.Key, out var username) ? username : "Anon", Score = kv.Value })
        };
    }

    #endregion
}
