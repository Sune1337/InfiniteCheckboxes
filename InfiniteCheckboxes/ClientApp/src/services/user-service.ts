import { inject, Injectable } from '@angular/core';
import { LocalUser } from './models/local-user';
import { UserApiService } from '../api/user-api.service';
import { getLocalUser, setLocalUser } from '../utils/user-utils';
import { ReplaySubject } from 'rxjs';
import { UserDetails } from '../api/models/user-details';

@Injectable({
  providedIn: 'root'
})
export class UserService {

  public localUser = new ReplaySubject<LocalUser>(1);

  private userApiService = inject(UserApiService);
  private _localUser: LocalUser;

  constructor() {
    this._localUser = getLocalUser()
    this.localUser.next(this._localUser);

    // Update user-details from server.
    this.userApiService
      .getUserDetails()
      .then(userDetails => {
        this._localUser = { ...this._localUser, ...userDetails };
        setLocalUser(this._localUser);
        this.localUser.next(this._localUser);
      });
  }

  public setUserDetails = (userDetails: UserDetails): Promise<void> => {
    // Update server with new user-details.
    return this.userApiService
      .setUserDetails(userDetails);
  }

}
