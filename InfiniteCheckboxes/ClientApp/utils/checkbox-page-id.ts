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
