namespace ValueDebouncer;

using Microsoft.Extensions.Logging;

public class DebounceValues<TValue>
{
    #region Constants

    private const int DefaultEmitDelay = 250;

    #endregion

    #region Fields

    private readonly int _emitDelay;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stopTasksTokenSource = new();
    private readonly Lock _valuesLock = new();
    private Task? _emitValuesTask;
    private TValue? _value;

    #endregion

    #region Constructors and Destructors

    public DebounceValues(ILogger logger, int emitDelay = DefaultEmitDelay)
    {
        _logger = logger;
        _emitDelay = emitDelay;
    }

    #endregion

    #region Delegates

    public delegate Task EmitValueDelegate(TValue value);

    #endregion

    #region Public Events

    public event EmitValueDelegate? EmitValue;

    #endregion

    #region Public Methods and Operators

    public void DebounceValue(TValue value)
    {
        lock (_valuesLock)
        {
            _value = value;
            _emitValuesTask ??= EmitValueTask();
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

    private async Task EmitValueTask()
    {
        try
        {
            await Task.Delay(_emitDelay, _stopTasksTokenSource.Token);

            TValue? localValue;
            lock (_valuesLock)
            {
                localValue = _value;
                _value = default;
                _emitValuesTask = null;
            }

            if (localValue != null && EmitValue != null)
            {
                await EmitValue.Invoke(localValue);
            }
        }

        catch (TaskCanceledException)
        {
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception caught in DebounceValues.EmitValueTask: {ExceptionMessage}.", ex.Message);
        }
    }

    #endregion
}
