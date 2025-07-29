import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from "rxjs";
import { War } from './models/war';

export type Wars = { [id: number]: War };

@Injectable({
  providedIn: 'root'
})
export class WarHubService {

  public wars: Subject<Wars> = new BehaviorSubject({});

  constructor() {
  }

  public subscribeToWar = (id: number): void => {

  }

  public unsubscribeToWar = (id: number): void => {

  }

  public getCurrentWar = async (): Promise<number> => {
    return 0;
  }

  private whenSubscribed = (): void => {

  }

  private whenUnsubscribed = (): void => {

  }
}
