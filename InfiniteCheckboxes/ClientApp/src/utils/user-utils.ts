import { LocalUser } from "../services/models/local-user";

export function getLocalUser(): LocalUser {
  // Get user from local storage.
  const userJson = localStorage.getItem('user');
  let localUser = !userJson ? null : JSON.parse(userJson) as LocalUser;
  if (!localUser) {
    // Create and save user in local storage.
    localUser = { userId: generateUserId() };
    localStorage.setItem('user', JSON.stringify(localUser));
  }

  return localUser;
}

export function setLocalUser(localUser: LocalUser): void {
  localStorage.setItem('user', JSON.stringify(localUser));
}

export function getLocalUserId(): string {
  return getLocalUser().userId;
}

function generateUserId(): string {
  // Create an array of 32 bytes (256 bits)
  const array = new Uint8Array(32);

  // Fill it with cryptographically secure random values
  crypto.getRandomValues(array);

  // Convert to hex string
  return Array.from(array)
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
}
