namespace BitCoding;

public static class IndexAndBoolCoder
{
    #region Public Methods and Operators

    public static IEnumerable<KeyValuePair<int, byte>> Decode(byte[] encodedData)
    {
        var bitPosition = 0;

        while (bitPosition + 12 < encodedData.Length * 8)
        {
            var combined = 0;

            // Read 13 bits
            for (var i = 0; i < 13; i++)
            {
                var byteIndex = bitPosition / 8;
                var bitOffset = bitPosition % 8;

                if (byteIndex >= encodedData.Length)
                    break;

                var bit = (encodedData[byteIndex] >> bitOffset) & 1;
                combined |= bit << i;

                bitPosition++;
            }

            var value = (byte)(combined & 1);
            var index = combined >> 1;

            yield return new KeyValuePair<int, byte>(index, value);
        }
    }

    public static byte[] Encode(ICollection<KeyValuePair<int, byte>> items)
    {
        // Each checkbox needs 13 bits (12 for index + 1 for value)
        // We'll pack these into bytes, so calculate the required size
        var totalBits = items.Count * 13;
        var byteArraySize = (totalBits + 7) / 8; // Round up to nearest byte

        var result = new byte[byteArraySize];

        // Calculate which byte we're currently writing to
        var byteIndex = 0;
        var bitOffset = 0;

        foreach (var (index, value) in items)
        {
            if (index < 0 || index >= 4096)
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be between 0 and 4095");
            if (value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be 0 or 1");

            // Combine index and value into 13 bits
            var combined = (index << 1) | value;

            // Write the 13 bits across the necessary bytes
            for (var i = 0; i < 13; i++)
            {
                if (byteIndex >= result.Length)
                    break;

                var bit = (combined >> i) & 1;
                if (bit == 1)
                {
                    result[byteIndex] |= (byte)(1 << bitOffset);
                }

                bitOffset++;
                if (bitOffset == 8)
                {
                    bitOffset = 0;
                    byteIndex++;
                }
            }
        }

        return result;
    }

    #endregion
}
