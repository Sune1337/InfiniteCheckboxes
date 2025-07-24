namespace BitCoding;

public static class BitArrayCoder
{
    #region Constants

    private const byte CountMask = 0x3F; // 0011_1111 - count for RLE
    private const byte DataMask = 0x7F; // 0111_1111 - literal data bits
    private const byte TypeMask = 0x80; // 1000_0000 - Block type (1=RLE, 0=Literal)
    private const byte ValueMask = 0x40; // 0100_0000 - RLE value bit

    #endregion

    #region Public Methods and Operators

    public static byte[]? Compress(bool[]? data)
    {
        if (data == null || data.Length == 0)
            return null;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(data.Length);

        var i = 0;
        while (i < data.Length)
        {
            // Count how many same values we have
            var value = data[i];
            var runLength = 1;
            while (i + runLength < data.Length &&
                   data[i + runLength] == value &&
                   runLength < 63)
            {
                runLength++;
            }

            // If we have enough repeated values AND it would save space
            // compared to literal encoding, use RLE
            if (runLength > 7)
            {
                // Write RLE block
                var rleByte = TypeMask; // Mark as RLE
                if (value) rleByte |= ValueMask; // Set value bit if true
                rleByte |= (byte)(runLength & CountMask); // Add count
                writer.Write(rleByte);
                i += runLength;
            }
            else
            {
                // Pack up to 7 bits into a literal byte
                byte literalByte = 0; // First bit is 0 (literal marker)
                int bitsToWrite = Math.Min(7, data.Length - i);

                for (int bit = 0; bit < bitsToWrite; bit++)
                {
                    if (data[i + bit])
                        literalByte |= (byte)(1 << (6 - bit));
                }

                writer.Write(literalByte);
                i += bitsToWrite;
            }
        }


        return ms.ToArray();
    }


    public static bool[]? Decompress(byte[]? compressed)
    {
        if (compressed == null || compressed.Length < 4)
        {
            return null;
        }

        // Read total length (first 4 bytes, little-endian)
        var totalLength =
            compressed[0] |
            (compressed[1] << 8) |
            (compressed[2] << 16) |
            (compressed[3] << 24);

        var result = new bool[totalLength];
        var resultIndex = 0;

        // Start after the length bytes
        var i = 4;
        while (i < compressed.Length && resultIndex < totalLength)
        {
            var header = compressed[i++];

            if ((header & TypeMask) != 0)
            {
                // RLE block
                var value = (header & ValueMask) != 0;
                var count = header & CountMask;

                for (var j = 0; j < count && resultIndex < totalLength; j++)
                {
                    result[resultIndex++] = value;
                }
            }
            else
            {
                // Literal block - unpack 7 bits of data
                var data = (byte)(header & DataMask);
                var bitsToRead = Math.Min(7, totalLength - resultIndex);

                for (var bit = 6; bit >= 7 - bitsToRead; bit--)
                {
                    result[resultIndex++] = ((data >> bit) & 1) == 1;
                }
            }
        }

        return result;
    }

    #endregion
}
