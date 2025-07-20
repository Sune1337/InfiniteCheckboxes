namespace RngWithSecret;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RngWithSecret.Options;

public class SecretPcg32Factory
{
    #region Fields

    private readonly Dictionary<string, SecretPcg32> _instanceCache = new();
    private readonly Lock _instanceCacheLock = new();
    private readonly IOptionsMonitor<SecretPcg32Options> _optionsMonitor;
    private readonly ILogger<SecretPcg32> _secretPcf32Logger;

    #endregion

    #region Constructors and Destructors

    public SecretPcg32Factory(IOptionsMonitor<SecretPcg32Options> optionsMonitor, ILogger<SecretPcg32> secretPcf32Logger)
    {
        _optionsMonitor = optionsMonitor;
        _secretPcf32Logger = secretPcf32Logger;
    }

    #endregion

    #region Public Methods and Operators

    public SecretPcg32 Create(string name)
    {
        lock (_instanceCacheLock)
        {
            if (_instanceCache.TryGetValue(name, out var instance))
            {
                return instance;
            }

            var options = _optionsMonitor.Get(name);
            instance = new SecretPcg32(options, _secretPcf32Logger);
            _instanceCache.Add(name, instance);
            return instance;
        }
    }

    #endregion
}
