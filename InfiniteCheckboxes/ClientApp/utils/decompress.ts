/**
 * Decompresses a byte array into a boolean array using hybrid RLE/literal decompression
 * @param compressed The compressed byte array
 * @returns Array of booleans
 */
export function decompressBitArray(compressed: Uint8Array): boolean[] {
  // Constants matching C# implementation
  const TYPE_MASK = 0x80;     // 1000_0000 - Block type (1=RLE, 0=Literal)
  const VALUE_MASK = 0x40;    // 0100_0000 - RLE value bit
  const COUNT_MASK = 0x3F;    // 0011_1111 - count for RLE
  const DATA_MASK = 0x7F;     // 0111_1111 - literal data bits

  if (!compressed || compressed.length < 4) {
    return [];
  }

  // Read total length (first 4 bytes, little-endian)
  const totalLength =
    compressed[0] |
    (compressed[1] << 8) |
    (compressed[2] << 16) |
    (compressed[3] << 24);

  const result: boolean[] = new Array(totalLength);
  let resultIndex = 0;

  // Start after the length bytes
  let i = 4;
  while (i < compressed.length && resultIndex < totalLength) {
    const header = compressed[i++];

    if ((header & TYPE_MASK) !== 0) {
      // RLE block
      const value = (header & VALUE_MASK) !== 0;
      const count = header & COUNT_MASK;

      for (let j = 0; j < count && resultIndex < totalLength; j++) {
        result[resultIndex++] = value;
      }
    } else {
      // Literal block - unpack 7 bits of data
      const data = header & DATA_MASK;
      const bitsToRead = Math.min(7, totalLength - resultIndex);

      for (let bit = 6; bit >= 7 - bitsToRead; bit--) {
        result[resultIndex++] = ((data >> bit) & 1) === 1;
      }
    }
  }

  return result;
}
