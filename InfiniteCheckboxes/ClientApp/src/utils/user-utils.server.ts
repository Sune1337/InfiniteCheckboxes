import { LocalUser } from "../services/models/local-user";

export function getLocalUser(): LocalUser {
  return {
    userId: '00',
    userName: 'Joe'
  };
}

export function setLocalUser(localUser: LocalUser): void {

}

export function getLocalUserId(): string {
  return getLocalUser().userId;
}
