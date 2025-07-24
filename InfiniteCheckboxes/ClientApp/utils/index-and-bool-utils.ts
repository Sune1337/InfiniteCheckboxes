/**
 * Decodes a byte array into an array of [index, value] pairs
 * @param {Uint8Array} encodedData - The encoded byte array
 * @returns {Array<[number, boolean]>} Array of [index, value] pairs
 */
export function decompressIndexAndBoolArray(encodedData: Uint8Array): [number, boolean][] {
  const result = [] as [number, boolean][];
  let bitPosition = 0;

  while (bitPosition + 12 < encodedData.length * 8) {
    let combined = 0;

    // Read 13 bits
    for (let i = 0; i < 13; i++) {
      const byteIndex = Math.floor(bitPosition / 8);
      const bitOffset = bitPosition % 8;

      if (byteIndex >= encodedData.length) {
        break;
      }

      const bit = (encodedData[byteIndex] >> bitOffset) & 1;
      combined |= bit << i;

      bitPosition++;
    }

    const value = (combined & 1) === 1;
    const index = combined >> 1;

    result.push([index, value]);
  }

  return result;
}

/**
 * Encodes an array of [index, value] pairs into a byte array
 * @param {Array<[number, boolean]>} items - Array of [index, value] pairs
 * @returns {Uint8Array} Encoded byte array
 */
export function compressIndexAndBoolArray(items: Array<[number, boolean]>): Uint8Array {
  // Each item needs 13 bits (12 for index + 1 for value)
  const totalBits = items.length * 13;
  const byteArraySize = Math.ceil(totalBits / 8); // Round up to nearest byte

  const result = new Uint8Array(byteArraySize);

  let byteIndex = 0;
  let bitOffset = 0;

  for (const [index, value] of items) {
    if (index < 0 || index >= 4096) {
      throw new Error("Index must be between 0 and 4095");
    }
    if (typeof value === "number" && value > 1) {
      throw new Error("Value must be 0 or 1");
    }

    // Combine index and value into 13 bits
    const combined = (index << 1) | (value ? 1 : 0);

    // Write the 13 bits across the necessary bytes
    for (let i = 0; i < 13; i++) {
      if (byteIndex >= result.length) {
        break;
      }

      const bit = (combined >> i) & 1;
      if (bit === 1) {
        result[byteIndex] |= (1 << bitOffset);
      }

      bitOffset++;
      if (bitOffset === 8) {
        bitOffset = 0;
        byteIndex++;
      }
    }
  }

  return result;
}

