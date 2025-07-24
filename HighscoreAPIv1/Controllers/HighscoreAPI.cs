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
    public async Task<IEnumerable<Highscore>> GetHighscores()
    {
        return await _cache.GetOrAddAsync("Highscores", LoadHighscoreData, DateTime.UtcNow.AddSeconds(10));
    }

    #endregion

    #region Methods

    private async Task<IEnumerable<Highscore>> LoadHighscoreData()
    {
        var checkedHighscoreGrain = _grainFactory.GetGrain<IHighscoreGrain>(HighscoreLists.Checked);
        var uncheckedHighscoreGrain = _grainFactory.GetGrain<IHighscoreGrain>(HighscoreLists.Unchecked);
        var goldDiggerHighscoreGrain = _grainFactory.GetGrain<IHighscoreGrain>(HighscoreLists.GoldDigger);

        Task<Dictionary<string, ulong>> checkedHighscoresTask;
        Task<Dictionary<string, ulong>> uncheckedHighscoresTask;
        Task<Dictionary<string, ulong>> goldDiggerHighscoreTask;
        await Task.WhenAll(
            checkedHighscoresTask = checkedHighscoreGrain.GetScores(),
            uncheckedHighscoresTask = uncheckedHighscoreGrain.GetScores(),
            goldDiggerHighscoreTask = goldDiggerHighscoreGrain.GetScores()
        );

        var checkedHighscores = checkedHighscoresTask.Result;
        var uncheckedHighscores = uncheckedHighscoresTask.Result;
        var goldDiggerHighscores = goldDiggerHighscoreTask.Result;

        var userIds = ((Dictionary<string, ulong>[]) [checkedHighscores, uncheckedHighscores, goldDiggerHighscores])
            .SelectMany(d => d.Keys)
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

        return
        [
            new Highscore
            {
                Name = HighscoreLists.Checked,
                Scores = checkedHighscores.Select(kv => new UserScore { Username = usernames.TryGetValue(kv.Key, out var username) ? username : "Anon", Score = kv.Value })
            },
            new Highscore
            {
                Name = HighscoreLists.Unchecked,
                Scores = uncheckedHighscores.Select(kv => new UserScore { Username = usernames.TryGetValue(kv.Key, out var username) ? username : "Anon", Score = kv.Value })
            },
            new Highscore
            {
                Name = HighscoreLists.GoldDigger,
                Scores = goldDiggerHighscores.Select(kv => new UserScore { Username = usernames.TryGetValue(kv.Key, out var username) ? username : "Anon", Score = kv.Value })
            }
        ];
    }

    #endregion
}
