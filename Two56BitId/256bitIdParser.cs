namespace Two56bitId;

public static class Two56BitIdParser
{
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

    public static bool TryParse256BitBase64Id(this string? base64Id, out string parsedId)
    {
        parsedId = string.Empty;

        if (string.IsNullOrWhiteSpace(base64Id))
        {
            return false;
        }

        // Max base64 length of 256bits data is 44 chars including padding.
        if (base64Id.Length > 44)
        {
            return false;
        }

        try
        {
            parsedId = Convert.ToHexStringLower(Convert.FromBase64String(base64Id)).TrimLeadingZeroPairs();
            return true;
        }

        catch
        {
            return false;
        }
    }

    #endregion

    #region Methods

    private static int GetHexVal(char hex)
    {
        var val = (int)hex;
        return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
    }

    #endregion
}
