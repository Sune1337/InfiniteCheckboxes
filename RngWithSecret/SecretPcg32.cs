namespace RngWithSecret;

using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using RandN.Rngs;

using RngWithSecret.Options;

using Two56bitId;

public class SecretPcg32
{
    #region Fields

    private readonly byte[] _secret;

    #endregion

    #region Constructors and Destructors

    public SecretPcg32(SecretPcg32Options options, ILogger<SecretPcg32> logger)
    {
        if (options.Secret is null)
        {
            throw new ArgumentNullException(nameof(options.Secret), "Secret must be set.");
        }

        _secret = options.Secret.HexStringToByteArray();
        if (_secret.Length <= 0)
        {
            throw new ArgumentException("Secret must be more than 0 bytes.", nameof(options.Secret));
        }

        if (_secret.All(b => b == 0))
        {
            logger.LogWarning("Secret is all zeros. This is not recommended.");
        }
    }

    #endregion

    #region Public Methods and Operators

    public Pcg32 GetRngForAddress(string address)
    {
        // Parse address.
        Span<byte> addressBytes = stackalloc byte[32];
        var addressLength = address.HexStringToByteArray(addressBytes, 32);

        // Use span-based HMAC for better performance
        Span<byte> mixedBytes = stackalloc byte[32];
        using var hmac = new HMACSHA256(_secret);
        if (!hmac.TryComputeHash(addressBytes[..addressLength], mixedBytes, out _))
        {
            throw new InvalidOperationException("Failed to compute hash");
        }

        // Extract seed and stream.
        var seed = BitConverter.ToUInt64(mixedBytes[..8]);
        var stream = BitConverter.ToUInt64(mixedBytes[8..16]);

        return Pcg32.Create(seed, stream);
    }

    #endregion
}
