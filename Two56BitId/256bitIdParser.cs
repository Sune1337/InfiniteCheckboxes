namespace Two56bitId;

using System.Text.RegularExpressions;

public static partial class Two56BitIdParser
{
    #region Static Fields

    private static readonly Regex Two56BitHexStringRegex = Two56BitHexStringRegexFunc();

    #endregion

    #region Public Methods and Operators

    /// <summary>
    /// From: https://stackoverflow.com/questions/321370/how-can-i-convert-a-hex-string-to-a-byte-array
    /// </summary>
    public static byte[] HexStringToByteArray(this string hex)
    {
        if (hex.Length % 2 == 1)
        {
            throw new Exception("The binary key cannot have an odd number of digits");
        }

        var arr = new byte[hex.Length >> 1];
        for (var i = 0; i < hex.Length >> 1; ++i)
        {
            arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
        }

        return arr;
    }

    /// <summary>
    /// From: https://stackoverflow.com/questions/321370/how-can-i-convert-a-hex-string-to-a-byte-array
    /// </summary>
    public static int HexStringToByteArray(this string hex, Span<byte> buffer, int size)
    {
        if (hex.Length % 2 == 1)
        {
            throw new Exception("The binary key cannot have an odd number of digits");
        }

        var s = 0;
        for (var i = 0; i < hex.Length >> 1; ++i)
        {
            s += 1;
            if (s > size)
            {
                throw new Exception("buffer too small");
            }

            buffer[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
        }

        return s;
    }

    public static string TrimLeadingZeroPairs(this string hex)
    {
        int startIndex = 0;

        // Move index forward while we see "00" pairs
        while (startIndex < hex.Length - 1 && // Ensure we have at least 2 chars left
               hex[startIndex] == '0' &&
               hex[startIndex + 1] == '0')
        {
            startIndex += 2;
        }

        // If we consumed the entire string, return "00"
        if (startIndex >= hex.Length)
            return "00";

        return hex.Substring(startIndex);
    }

    public static bool Validate256BitHex(this string? hexString)
    {
        return hexString != null && Two56BitHexStringRegex.IsMatch(hexString);
    }

    #endregion

    #region Methods

    private static int GetHexVal(char hex)
    {
        var val = (int)hex;
        return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
    }

    [GeneratedRegex("^[0-9a-fA-F]{1,64}$")]
    private static partial Regex Two56BitHexStringRegexFunc();

    #endregion
}
