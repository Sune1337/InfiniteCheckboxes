namespace MinesweeperGrain;

using BitCoding;

using global::MinesweeperGrain.Models;

using GrainInterfaces;
using GrainInterfaces.Highscore;
using GrainInterfaces.Minesweeper;
using GrainInterfaces.Minesweeper.Models;

using RandN.Rngs;

using RedisMessages.MinesweeperUpdate;

using RngWithSecret;

using Two56bitId;

public class MinesweeperGrain : Grain, IMinesweeperGrain, ICheckboxCallbackGrain
{
    #region Fields

    private readonly IPersistentState<MinesweeperState> _minesweeperState;
    private readonly IRedisMinesweeperUpdatePublisherManager _redisMinesweeperUpdatePublisherManager;
    private readonly SecretPcg32Factory _secretPcg32Factory;

    private bool[]? _flags;
    private string _grainId = null!;
    private Pcg32? _minesweeperRng;

    #endregion

    #region Constructors and Destructors

    public MinesweeperGrain(
        [PersistentState("MinesweeperState", "MinesweeperStore")]
        IPersistentState<MinesweeperState> minesweeperState,
        SecretPcg32Factory secretPcg32Factory,
        IRedisMinesweeperUpdatePublisherManager redisMinesweeperUpdatePublisherManager
    )
    {
        _minesweeperState = minesweeperState;
        _secretPcg32Factory = secretPcg32Factory;
        _redisMinesweeperUpdatePublisherManager = redisMinesweeperUpdatePublisherManager;
    }

    #endregion

    #region Properties

    private Pcg32 MinesweeperRng => _minesweeperRng ??= _secretPcg32Factory.Create("MinesweeperRng").GetRngForAddress(_grainId);

    #endregion

    #region Public Methods and Operators

    public async Task CreateGame(uint width, uint numberOfMines, string userId, bool luckyStart)
    {
        if (width < 8 || width > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var gameSize = width * width;
        var minMines = width * width * 0.125;
        if (numberOfMines < minMines || numberOfMines > gameSize * 0.2)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfMines));
        }

        _minesweeperState.State.CreatedUtc = DateTime.UtcNow;
        _minesweeperState.State.Width = width;
        _minesweeperState.State.UserId = userId;
        _minesweeperState.State.SweepLocationId = Random256Bit.GenerateHex();
        _minesweeperState.State.FlagLocationId = Random256Bit.GenerateHex();

        // Create mines.
        GenerateMines(gameSize, numberOfMines);

        // Get the checkbox-grains and register this game for callbacks.
        var sweepGrain = GrainFactory.GetGrain<ICheckboxGrain>(_minesweeperState.State.SweepLocationId);
        await sweepGrain.RegisterCallback<MinesweeperGrain>(this.GetGrainId());
        var flagGrain = GrainFactory.GetGrain<ICheckboxGrain>(_minesweeperState.State.FlagLocationId);
        await flagGrain.RegisterCallback<MinesweeperGrain>(this.GetGrainId());

        if (luckyStart)
        {
            await LuckyStart(_minesweeperState.State.SweepLocationId, width, userId);
        }
        else
        {
            await _minesweeperState.WriteStateAsync();
        }
    }

    public async Task<Minesweeper> GetMinesweeper()
    {
        if (_minesweeperState.State.Width == null)
        {
            throw new Exception("Width is null.");
        }

        var width = _minesweeperState.State.Width.Value;
        var gameSize = width * width;

        if (_minesweeperState.State.SweepLocationId == null)
        {
            throw new Exception("SweepLocationId is null.");
        }

        // Count surronding mines.
        var checkboxGrain = GrainFactory.GetGrain<ICheckboxGrain>(_minesweeperState.State.SweepLocationId);
        var checkboxes = BitArrayCoder.Decompress(await checkboxGrain.GetCheckboxes());
        if (checkboxes == null)
        {
            return MinesweeperStateToMinesweeper();
        }

        var mineCounts = new Dictionary<int, int>();
        for (var i = 0; i < gameSize; i++)
        {
            if (checkboxes[i])
            {
                var count = CountSurroundingMines(_minesweeperState.State.Mines, (uint)i, width);
                if (count > 0)
                {
                    mineCounts.Add(i, count);
                }
            }
        }

        return MinesweeperStateToMinesweeper(mineCounts);
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _grainId = this.GetPrimaryKeyString();
        return Task.CompletedTask;
    }

    public async Task<Dictionary<int, bool>?> WhenCheckboxesUpdated(string id, bool[] checkboxes, int index, bool value, string userId)
    {
        if (_minesweeperState.State.EndUtc != null)
        {
            throw new Exception("The game has ended!");
        }

        if (_minesweeperState.State.Width == null)
        {
            throw new Exception("Width is null.");
        }

        var width = _minesweeperState.State.Width.Value;
        var gameSize = width * width;
        if (index < 0 || index >= gameSize)
        {
            throw new Exception("Stick to the game-area!");
        }

        if (userId != _minesweeperState.State.UserId)
        {
            throw new Exception("Not your game!");
        }

        // Register when war started.
        _minesweeperState.State.StartUtc ??= DateTime.UtcNow;

        if (id == _minesweeperState.State.FlagLocationId)
        {
            // User flagged something.
            checkboxes[index] = value;
            _flags = checkboxes;
            return null;
        }

        if (!value)
        {
            // User unchecked something.
            return null;
        }

        var uIndex = (uint)index;
        var isFirstSweep = IsFirstSweep(checkboxes);
        if (isFirstSweep && _minesweeperState.State.Mines.ContainsKey(uIndex))
        {
            // The user hit a mine on the first click. Move the mine to first free spot.
            MoveMineToFirstFree(uIndex);
            await _minesweeperState.WriteStateAsync();
        }

        // Check the current location.
        checkboxes[index] = true;

        if (_minesweeperState.State.Mines.ContainsKey(uIndex))
        {
            // User hit a mine!
            _minesweeperState.State.Mines[uIndex] = true;
            _minesweeperState.State.EndUtc = DateTime.UtcNow;
            await _minesweeperState.WriteStateAsync();
            await _redisMinesweeperUpdatePublisherManager.PublishMinesweeperAsync(_grainId, MinesweeperStateToMinesweeper());
            return null;
        }

        // Count surrounding mines.
        var mineCounts = new Dictionary<int, int>();
        Dictionary<int, bool>? result = null;
        var numberOfSurroundingMines = CountSurroundingMines(_minesweeperState.State.Mines, uIndex, width);
        if (numberOfSurroundingMines > 0)
        {
            mineCounts.Add(index, numberOfSurroundingMines);
        }
        else
        {
            // Autoplay.
            var sweeped = AutoPlay(checkboxes, _flags, _minesweeperState.State.Mines, uIndex, width);
            result = sweeped.ToDictionary(x => (int)x, _ => true);

            // Add mine-counts.
            foreach (var u in sweeped)
            {
                var count = CountSurroundingMines(_minesweeperState.State.Mines, u, width);
                if (count > 0)
                {
                    mineCounts.Add((int)u, count);
                }
            }
        }

        if (mineCounts.Count > 0)
        {
            // Publish new counts.
            await _redisMinesweeperUpdatePublisherManager.PublishCountsAsync(_grainId, mineCounts);
        }

        // Count checks to see is game is done.
        var countChecked = 0;
        for (var i = 0; i < gameSize; i++)
        {
            if (checkboxes[i])
            {
                countChecked++;
            }
        }

        if (countChecked == gameSize - _minesweeperState.State.Mines.Count)
        {
            // User finished!
            _minesweeperState.State.EndUtc = DateTime.UtcNow;

            // Calculate score
            var mineDensity = _minesweeperState.State.Mines.Count / (double)gameSize;
            var sizeRatio = width / 8.0;
            var playTime = (_minesweeperState.State.EndUtc.Value - _minesweeperState.State.StartUtc.Value).TotalSeconds;
            var smallBoardPunishment = Math.Log(gameSize - 62, 4096);
            _minesweeperState.State.Score = (ulong)Math.Round(mineDensity * sizeRatio / playTime * smallBoardPunishment * 100000);

            if (!(isFirstSweep && _minesweeperState.State.IsLuckyStart) && _minesweeperState.State.UserId != null)
            {
                if (isFirstSweep)
                {
                    // Only record to high-score list if this was not a lucky start.
                    var minesweeperHighscoreGrain = GrainFactory.GetGrain<IHighscoreCollectorGrain>(HighscoreLists.MinesweeperOneClickSweep);
                    await minesweeperHighscoreGrain.UpdateScore(_minesweeperState.State.UserId, (ulong)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds);
                }
                else if (_minesweeperState.State.IsLuckyStart)
                {
                    // Update high score.
                    var minesweeperHighscoreGrain = GrainFactory.GetGrain<IHighscoreCollectorGrain>(HighscoreLists.MinesweeperLuckyStart);
                    await minesweeperHighscoreGrain.UpdateScore(_minesweeperState.State.UserId, _minesweeperState.State.Score.Value);
                }
                else
                {
                    // Update high score.
                    var minesweeperHighscoreGrain = GrainFactory.GetGrain<IHighscoreCollectorGrain>(HighscoreLists.Minesweeper);
                    await minesweeperHighscoreGrain.UpdateScore(_minesweeperState.State.UserId, _minesweeperState.State.Score.Value);
                }
            }

            // Save state and publish.
            await _minesweeperState.WriteStateAsync();
            await _redisMinesweeperUpdatePublisherManager.PublishMinesweeperAsync(_grainId, MinesweeperStateToMinesweeper());
        }

        return result;
    }

    #endregion

    #region Methods

    private List<uint> AutoPlay(bool[]? checkboxes, bool[]? flags, Dictionary<uint, bool> mines, uint index, uint stride, List<uint>? autoChecked = null, HashSet<uint>? sweeped = null, Queue<uint>? cellsToProcess = null)
    {
        autoChecked ??= new List<uint>();
        sweeped ??= new HashSet<uint>();
        cellsToProcess ??= new Queue<uint>();
        var gameSize = stride * stride;

        // Add initial cell
        autoChecked.Add(index);
        sweeped.Add(index);
        cellsToProcess.Enqueue(index);

        // Process cells with 0 surrounding mines
        while (cellsToProcess.Count > 0)
        {
            var currentIndex = cellsToProcess.Dequeue();

            foreach (var neighborIndex in IterateSurroundingIndexes(currentIndex, stride))
            {
                if ((flags != null && flags[neighborIndex]) || neighborIndex >= gameSize || !sweeped.Add(neighborIndex))
                {
                    continue;
                }

                if ((checkboxes == null || !checkboxes[neighborIndex]) && CountSurroundingMines(mines, neighborIndex, stride) == 0)
                {
                    if (checkboxes != null)
                    {
                        checkboxes[(int)neighborIndex] = true;
                    }

                    autoChecked.Add(neighborIndex);
                    cellsToProcess.Enqueue(neighborIndex);
                }
            }
        }

        // Check all items surrounding spots with 0 neighbors
        var initialCount = autoChecked.Count;
        for (var i = 0; i < initialCount; i++)
        {
            var ac = autoChecked[i];
            foreach (var neighborIndex in IterateSurroundingIndexes(ac, stride))
            {
                if ((flags == null || !flags[neighborIndex]) && (checkboxes == null || !checkboxes[neighborIndex]) && !autoChecked.Contains(neighborIndex))
                {
                    if (checkboxes != null)
                    {
                        checkboxes[(int)neighborIndex] = true;
                    }

                    autoChecked.Add(neighborIndex);
                }
            }
        }

        return autoChecked;
    }

    private int CountSurroundingMines(Dictionary<uint, bool> mines, uint index, uint stride)
    {
        var count = 0;
        foreach (var neighborIndex in IterateSurroundingIndexes(index, stride))
        {
            if (mines.ContainsKey(neighborIndex))
            {
                count++;
            }
        }

        return count;
    }

    private void GenerateMines(uint gameSize, uint numberOfMines)
    {
        var mines = _minesweeperState.State.Mines;
        while (mines.Count < numberOfMines)
        {
            var index = MinesweeperRng.NextUInt32() % gameSize;
            mines.TryAdd(index, false);
        }
    }

    private bool IsFirstSweep(bool[] game)
    {
        var gameSize = _minesweeperState.State.Width * _minesweeperState.State.Width;
        for (var i = 0; i < gameSize; i++)
        {
            if (game[i])
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<uint> IterateSurroundingIndexes(uint index, uint stride, bool includeDiagonals = true)
    {
        var maxIndex = stride * stride;

        // Calculate row and column from index
        var row = index / stride;
        var col = index % stride;

        // Define the range based on whether diagonals should be included
        for (var r = -1; r <= 1; r++)
        {
            for (var c = -1; c <= 1; c++)
            {
                // Skip the center position (current index)
                if (r == 0 && c == 0)
                    continue;

                // Skip diagonals if not included
                if (!includeDiagonals && Math.Abs(r) + Math.Abs(c) == 2)
                    continue;

                // Calculate neighbor position
                var newRow = row + r;
                var newCol = col + c;

                // Skip if outside the grid bounds
                if (newRow < 0 || newRow >= maxIndex / stride ||
                    newCol < 0 || newCol >= stride)
                    continue;

                // Calculate array index and yield it
                var neighborIndex = (newRow * stride) + newCol;
                yield return (uint)neighborIndex;
            }
        }
    }

    private async Task LuckyStart(string sweepLocationId, uint width, string userId)
    {
        _minesweeperState.State.IsLuckyStart = true;

        // Find the biggest area to flood-fill.
        var testedIndexes = new HashSet<uint>();
        var gameSize = width * width;
        var largestArea = 0;
        var largestAreaIndex = 0;

        var autoChecked = new List<uint>();
        var sweeped = new HashSet<uint>();
        var cellsToProcess = new Queue<uint>();

        for (var index = 0u; index < gameSize; index++)
        {
            if (testedIndexes.Add(index) == false)
            {
                continue;
            }

            var surroundingMines = CountSurroundingMines(_minesweeperState.State.Mines, index, width);
            if (surroundingMines > 0)
            {
                continue;
            }

            var filledIndexes = AutoPlay(null, null, _minesweeperState.State.Mines, index, width, autoChecked: autoChecked, sweeped: sweeped, cellsToProcess: cellsToProcess);
            for (var i = 0; i < filledIndexes.Count; i++)
            {
                var filledIndex = filledIndexes[i];
                testedIndexes.Add(filledIndex);
            }

            if (filledIndexes.Count > largestArea)
            {
                largestArea = filledIndexes.Count;
                largestAreaIndex = (int)index;
            }

            autoChecked.Clear();
            sweeped.Clear();
            cellsToProcess.Clear();
        }

        using var scope = RequestContext.AllowCallChainReentrancy();
        var checkboxGrain = GrainFactory.GetGrain<ICheckboxGrain>(sweepLocationId);
        await checkboxGrain.SetCheckbox(largestAreaIndex, 1, userId);
    }


    private Minesweeper MinesweeperStateToMinesweeper(Dictionary<int, int>? mineCounts = null)
    {
        return new Minesweeper
        {
            Id = _grainId,
            Width = _minesweeperState.State.Width ?? throw new Exception("Width is null"),
            FlagLocationId = _minesweeperState.State.FlagLocationId ?? throw new Exception("FlagLocationId is null"),
            SweepLocationId = _minesweeperState.State.SweepLocationId ?? throw new Exception("SweepLocationId is null"),
            CreatedUtc = _minesweeperState.State.CreatedUtc ?? throw new Exception("CreatedUtc is null"),
            StartUtc = _minesweeperState.State.StartUtc,
            EndUtc = _minesweeperState.State.EndUtc,
            Mines = _minesweeperState.State.EndUtc == null ? null : _minesweeperState.State.Mines.Select(kv => kv.Key).ToArray(),
            MineCounts = mineCounts,
            Score = _minesweeperState.State.Score
        };
    }

    private void MoveMineToFirstFree(uint index)
    {
        var gameSize = _minesweeperState.State.Width * _minesweeperState.State.Width;
        for (var i = 0u; i < gameSize; i++)
        {
            if (i != index && _minesweeperState.State.Mines.TryAdd(i, false))
            {
                _minesweeperState.State.Mines.Remove(index);
                return;
            }
        }
    }

    #endregion
}
