namespace CheckboxHubv1.Utils;

public delegate Task EmitValuesDelegate(Dictionary<int, byte> values);

public class DebounceValues
{
    #region Constants

    private const int EmitDelay = 500;

    #endregion

    #region Fields

    private readonly ILogger<DebounceValues> _logger;
    private readonly CancellationTokenSource _stopTasksTokenSource = new();
    private readonly Lock _valuesLock = new();
    private Task? _emitValuesTask;
    private Dictionary<int, byte> _values = [];

    #endregion

    #region Constructors and Destructors

    public DebounceValues(ILogger<DebounceValues> logger)
    {
        _logger = logger;
        _emitValuesTask = EmitValues();
    }

    #endregion

    #region Public Events

    public event EmitValuesDelegate? EmitValuesDelegate;

    #endregion

    #region Public Methods and Operators

    public void DebounceValue(int index, byte value)
    {
        lock (_valuesLock)
        {
            _values[index] = value;
            _emitValuesTask ??= EmitValues();
        }
    }

    public async Task Stop()
    {
        if (_emitValuesTask != null)
        {
            await _stopTasksTokenSource.CancelAsync();
            await _emitValuesTask;
        }
    }

    #endregion

    #region Methods

    private async Task EmitValues()
    {
        try
        {
            await Task.Delay(EmitDelay, _stopTasksTokenSource.Token);

            Dictionary<int, byte> localValues;
            lock (_valuesLock)
            {
                localValues = _values;
                _values = new Dictionary<int, byte>();
                _emitValuesTask = null;
            }

            if (localValues.Count > 0 && EmitValuesDelegate != null)
            {
                await EmitValuesDelegate.Invoke(localValues);
            }
        }

        catch (TaskCanceledException)
        {
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception caught in DebounceValues.EmitValues: {ExceptionMessage}.", ex.Message);
        }
    }

    #endregion
}
