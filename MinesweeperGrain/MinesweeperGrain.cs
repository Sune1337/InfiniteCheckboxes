namespace MinesweeperGrain;

using BitCoding;

using global::MinesweeperGrain.Models;

using GrainInterfaces;
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

    public async Task CreateGame(uint width, uint numberOfMines, string userId)
    {
        if (width < 8 || width > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var gameSize = width * width;
        if (numberOfMines < 1 || numberOfMines > gameSize / 2)
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

        await _minesweeperState.WriteStateAsync();
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

    public async Task<Dictionary<int, bool>?> WhenCheckboxesUpdated(string id, bool[] checkboxes, int index, bool value)
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
        var battlefieldSize = width * width;
        if (index < 0 || index >= battlefieldSize)
        {
            throw new Exception("Stick to the game-area!");
        }

        // Register when war started.
        _minesweeperState.State.StartUtc ??= DateTime.UtcNow;

        if (id == _minesweeperState.State.FlagLocationId)
        {
            // User flagged something.
            return null;
        }

        if (!value)
        {
            // User unchecked something.
            return null;
        }

        var uIndex = (uint)index;
        if (IsFirstSweep(checkboxes) && _minesweeperState.State.Mines.ContainsKey(uIndex))
        {
            // The user hit a mine on the first click. Move the mine to first free spot.
            MoveMineToFirstFree(uIndex);
            await _minesweeperState.WriteStateAsync();
            var count = CountSurroundingMines(_minesweeperState.State.Mines, (uint)index, width);
            if (count > 0)
            {
                await _redisMinesweeperUpdatePublisherManager.PublishCountsAsync(_grainId, new Dictionary<int, int> { { index, count } });
            }

            return null;
        }

        // User sweeped something.
        if (_minesweeperState.State.Mines.ContainsKey(uIndex))
        {
            // User hit a mine!
            _minesweeperState.State.Mines[uIndex] = true;
            _minesweeperState.State.EndUtc = DateTime.UtcNow;
            await _minesweeperState.WriteStateAsync();
            await _redisMinesweeperUpdatePublisherManager.PublishMinesweeperAsync(_grainId, MinesweeperStateToMinesweeper());
            return null;
        }

        // Count checks to see is game is done.
        var countChecked = 1;
        for (var i = 0; i < battlefieldSize; i++)
        {
            if (checkboxes[i])
            {
                countChecked++;
            }
        }

        // Count surrounding mines.
        var mineCounts = new Dictionary<int, int>();
        Dictionary<int, bool>? result = null;
        var numberOfSurroundingMines = CountSurroundingMines(_minesweeperState.State.Mines, uIndex, width);
        if (numberOfSurroundingMines == 0)
        {
            // Autoplay.
            var sweeped = AutoPlay(checkboxes, _minesweeperState.State.Mines, uIndex, width);
            result = sweeped.ToDictionary(x => (int)x, x => true);

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

        var count2 = CountSurroundingMines(_minesweeperState.State.Mines, (uint)index, width);
        if (count2 > 0)
        {
            mineCounts.Add(index, count2);
        }

        if (mineCounts.Count > 0)
        {
            // Publish new counts.
            await _redisMinesweeperUpdatePublisherManager.PublishCountsAsync(_grainId, mineCounts);
        }

        if (countChecked == battlefieldSize - _minesweeperState.State.Mines.Count)
        {
            // User finished!
            _minesweeperState.State.EndUtc = DateTime.UtcNow;
            _minesweeperState.State.Win = true;
            await _minesweeperState.WriteStateAsync();
            await _redisMinesweeperUpdatePublisherManager.PublishMinesweeperAsync(_grainId, MinesweeperStateToMinesweeper());
            return null;
        }

        return result;
    }

    #endregion

    #region Methods

    private List<uint> AutoPlay(bool[] checkboxes, Dictionary<uint, bool> mines, uint index, uint stride)
    {
        var autoChecked = new List<uint>();
        var sweeped = new HashSet<uint>();
        var cellsToProcess = new Queue<uint>();

        // Add initial cell
        cellsToProcess.Enqueue(index);
        sweeped.Add(index);

        // Process cells with 0 surrounding mines
        while (cellsToProcess.Count > 0)
        {
            var currentIndex = cellsToProcess.Dequeue();

            foreach (var neighborIndex in IterateSurroundingIndexes(currentIndex, stride))
            {
                if (neighborIndex >= checkboxes.Length || !sweeped.Add(neighborIndex))
                {
                    continue;
                }

                if (!checkboxes[neighborIndex] && CountSurroundingMines(mines, neighborIndex, stride) == 0)
                {
                    checkboxes[(int)neighborIndex] = true;
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
                if (!checkboxes[neighborIndex] && !autoChecked.Contains(neighborIndex))
                {
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
            Win = _minesweeperState.State.Win == true
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
