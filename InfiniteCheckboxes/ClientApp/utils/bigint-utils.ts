export function bigIntToMinimalBytes(value: bigint): Uint8Array {
  if (value === 0n) {
    return new Uint8Array(1); // Just return a single zero byte
  }

  // Calculate how many bytes we need
  const bytes = [];
  while (value > 0n) {
    bytes.unshift(Number(value & 0xFFn)); // unshift instead of push for big-endian
    value = value >> 8n;
  }

  return new Uint8Array(bytes);
}

export function bytesToHexString(bytes: Uint8Array): string {
  return Array.from(bytes)
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
}

export function bigIntToHexString(id: bigint): string {
  if (id < 0) {
    throw new Error("Input must be a positive number");
  }

  // Convert to hex.
  let hexString = id.toString(16);

  // Check if the number is too large for 256 bits
  if (hexString.length > 64) {
    throw new Error("Number is too large to fit in 256 bits");
  }

  // Pad with leading zero if needed to ensure even length
  if (hexString.length % 2) {
    hexString = '0' + hexString;
  }

  return hexString;
}
