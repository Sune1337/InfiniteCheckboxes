import { Injectable, Output } from '@angular/core';
import { BehaviorSubject } from "rxjs";

export enum HubStatus {
  Idle,
  Connecting,
  Connected
}

@Injectable({
  providedIn: 'root'
})
export class HubStatusService {

  @Output()
  public HubStatus = new BehaviorSubject<HubStatus>(HubStatus.Idle);

  public SetStatus = (hubStatus: HubStatus): void => {
    this.HubStatus.next(hubStatus);
  }

}
