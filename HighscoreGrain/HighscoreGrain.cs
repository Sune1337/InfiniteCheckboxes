namespace HighscoreGrain;

using global::HighscoreGrain.Models;

using GrainInterfaces.Highscore;

public class HighscoreGrain : Grain, IHighscoreGrain
{
    #region Constants

    private const int NumberOfHighscoreItems = 100;

    #endregion

    #region Fields

    private readonly IPersistentState<HighscoreState> _highscoreState;

    #endregion

    #region Constructors and Destructors

    public HighscoreGrain(
        [PersistentState("HighscoreState", "HighscoreStore")]
        IPersistentState<HighscoreState> highscoreState)
    {
        _highscoreState = highscoreState;
    }

    #endregion

    #region Public Methods and Operators

    public async Task UpdateScore(Dictionary<string, ulong> scores)
    {
        // Remove users that already exist on the highscore list with a higher score.
        var userIds = Intersect(_highscoreState.State.Highscores, scores).ToList();
        for (var index = userIds.Count - 1; index >= 0; index--)
        {
            var userId = userIds[index];
            if (_highscoreState.State.Highscores[userId] >= scores[userId])
            {
                // User already has a higher score.
                userIds.RemoveAt(index);
            }
            else
            {
                // Update existing high-score.
                _highscoreState.State.Highscores[userId] = scores[userId];
            }

            scores.Remove(userId);
        }

        // Add rest of the scores to the highscore list.
        foreach (var score in scores)
        {
            _highscoreState.State.Highscores.Add(score.Key, score.Value);
        }

        // Keep top n scores.
        _highscoreState.State.Highscores = new Dictionary<string, ulong>(
            _highscoreState.State.Highscores
                .OrderByDescending(x => x.Value)
                .Take(NumberOfHighscoreItems)
        );

        await _highscoreState.WriteStateAsync();
    }

    #endregion

    #region Methods

    private static IEnumerable<TKey> Intersect<TKey, TValue>(Dictionary<TKey, TValue> first, Dictionary<TKey, TValue> second) where TKey : notnull
    {
        var leastItems = first.Count <= second.Count ? first : second;
        var mostItems = first.Count > second.Count ? first : second;

        foreach (var element in leastItems)
        {
            if (mostItems.ContainsKey(element.Key))
            {
                yield return element.Key;
            }
        }
    }

    #endregion
}
