import { Injectable } from '@angular/core';
import { LocalUser } from './models/local-user';
import { getLocalUser } from '#userUtils';
import { ReplaySubject } from 'rxjs';
import { UserDetails } from '../api/models/user-details';

@Injectable({
  providedIn: 'root'
})
export class UserService {

  public localUser = new ReplaySubject<LocalUser>(1);
  private _localUser: LocalUser;

  constructor() {
    this._localUser = getLocalUser()
    this.localUser.next(this._localUser);
  }

  public setUserDetails = async (userDetails: UserDetails): Promise<void> => {
  }

}
