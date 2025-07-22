import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UserDetails } from './models/user-details';

@Injectable({
  providedIn: 'root'
})
export class UserApiService {

  private httpClient = inject(HttpClient);

  public getUserDetails = (): Promise<UserDetails> => {
    return firstValueFrom(
      this.httpClient.get<UserDetails>('/api/v1/UserAPI/GetUserDetails')
    );
  }

  public setUserDetails = (userDetails: UserDetails): Promise<void> => {
    return firstValueFrom(
      this.httpClient.put<void>('/api/v1/UserAPI/SetUserDetails', userDetails)
    );
  }
}
