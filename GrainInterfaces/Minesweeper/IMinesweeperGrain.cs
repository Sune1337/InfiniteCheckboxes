namespace GrainInterfaces.Minesweeper;

using GrainInterfaces.Minesweeper.Models;

public interface IMinesweeperGrain : IGrainWithStringKey
{
    #region Public Methods and Operators

    public Task CreateGame(uint width, uint numberOfMines, string userId, bool luckyStart);
    public Task<Minesweeper> GetMinesweeper();

    #endregion
}
