namespace GoldDiggerGrain.Models;

public class GoldDiggerState
{
    #region Public Properties

    public Dictionary<int, bool> GoldSpots { get; set; } = new();

    #endregion
}
