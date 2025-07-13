namespace ValueDebouncer;

using Microsoft.Extensions.Logging;

public class DebounceValues<TKey, TValue> where TKey : notnull
{
    #region Constants

    private const int EmitDelay = 250;

    #endregion

    #region Fields

    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stopTasksTokenSource = new();
    private readonly Lock _valuesLock = new();
    private Task? _emitValuesTask;
    private Dictionary<TKey, TValue> _values = [];

    #endregion

    #region Constructors and Destructors

    public DebounceValues(ILogger logger)
    {
        _logger = logger;
        _emitValuesTask = EmitValuesTask();
    }

    #endregion

    #region Delegates

    public delegate Task EmitValuesDelegate(Dictionary<TKey, TValue> values);

    #endregion

    #region Public Events

    public event EmitValuesDelegate? EmitValues;

    #endregion

    #region Public Methods and Operators

    public void DebounceValue(TKey index, TValue value)
    {
        lock (_valuesLock)
        {
            _values[index] = value;
            _emitValuesTask ??= EmitValuesTask();
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

    private async Task EmitValuesTask()
    {
        try
        {
            await Task.Delay(EmitDelay, _stopTasksTokenSource.Token);

            Dictionary<TKey, TValue> localValues;
            lock (_valuesLock)
            {
                localValues = _values;
                _values = new Dictionary<TKey, TValue>();
                _emitValuesTask = null;
            }

            if (localValues.Count > 0 && EmitValues != null)
            {
                await EmitValues.Invoke(localValues);
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
