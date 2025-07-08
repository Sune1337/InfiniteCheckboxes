export function sortHex(hexArray: string[]) {
  return hexArray.sort((a, b) => {
    const aLower = a.toLowerCase();
    const bLower = b.toLowerCase();

    // Different lengths = longer is larger
    if (aLower.length !== bLower.length) {
      return aLower.length - bLower.length;
    }

    // Same length = lexicographical comparison
    return aLower.localeCompare(bLower);
  });
}
