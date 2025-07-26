namespace Two56bitId;

using System.Security.Cryptography;

public class Random256Bit
{
    #region Static Fields

    private static readonly byte[] Zero = [0];

    #endregion

    #region Public Methods and Operators

    public static ReadOnlySpan<byte> GenerateBytes()
    {
        // Create a byte array to hold 32 bytes (256 bits)
        var bytes = new byte[32];

        // Fill it with cryptographically strong random bytes
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Find first non-zero byte.
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != 0)
            {
                return bytes.AsSpan()[i..];
            }
        }

        return Zero.AsSpan();
    }

    public static string GenerateHex()
    {
        return Convert.ToHexStringLower(
            GenerateBytes()
        );
    }

    #endregion
}
