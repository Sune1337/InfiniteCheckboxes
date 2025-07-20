import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  public getUserId = (): string => {
    let userId = localStorage.getItem('userId');
    if (!userId) {
      userId = this.generateUserId();
      localStorage.setItem('userId', userId);
    }

    return userId;
  }

  private generateUserId = () => {
    // Create an array of 32 bytes (256 bits)
    const array = new Uint8Array(32);

    // Fill it with cryptographically secure random values
    crypto.getRandomValues(array);

    // Convert to hex string
    return Array.from(array)
      .map(b => b.toString(16).padStart(2, '0'))
      .join('');
  }
}
