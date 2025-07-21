export function bigIntToBase64(bigInt: bigint): string {
  // Convert to hex first, removing "0x" prefix if positive, or "-0x" if negative
  const isNegative = bigInt < 0n;
  let hex = bigInt.toString(16).replace(/^-/, '');

  // Ensure even length
  if (hex.length % 2) {
    hex = '0' + hex;
  }

  // Convert hex to Uint8Array
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < hex.length; i += 2) {
    bytes[i / 2] = parseInt(hex.substr(i, 2), 16);
  }

  // Add sign byte if negative
  const bytesWithSign = isNegative ?
    new Uint8Array([0x80, ...bytes]) :
    bytes;

  // Convert to base64
  return btoa(String.fromCharCode(...bytesWithSign));
}

export function base64ToBigInt(base64: string): bigint {
  // Convert base64 to bytes
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }

  // Check if number is negative (first byte is 0x80)
  const isNegative = bytes[0] === 0x80;
  const relevantBytes = isNegative ? bytes.slice(1) : bytes;

  // Convert to hex string
  let hex = Array.from(relevantBytes)
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');

  // Remove leading zeros, but keep at least one digit
  hex = hex.replace(/^0+/, '') || '0';

  // Convert to BigInt
  return BigInt(`${isNegative ? '-' : ''}0x${hex}`);
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

  return hexString;
}

export function base64ToUint8Array(base64: string): Uint8Array {
  const binaryString = window.atob(base64);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes;
}

export function base64ToHexString(base64: string): string {
  // Convert base64 to byte array
  const bytes = base64ToUint8Array(base64);

  // Convert byte array to hex string
  return Array.from(bytes)
    .map(byte => byte.toString(16).padStart(2, '0'))
    .join('');
}

