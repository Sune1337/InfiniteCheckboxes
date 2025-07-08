export function base64ToNumberArray(base64String: string): number[] {
  // First, create a binary string from the base64 string
  const binaryString = atob(base64String);

  // Create a Uint8Array of the same length as the binary string
  const result: number[] = [];

  // Convert each character to its byte value
  for (let i = 0; i < binaryString.length; i++) {
    result[i] = binaryString.charCodeAt(i);
  }

  return result;
}
