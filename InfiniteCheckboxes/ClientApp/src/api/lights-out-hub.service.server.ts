import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from "rxjs";
import { LightsOut } from './models/lights-out';

export type LightsOuts = { [id: string]: LightsOut };

@Injectable({
  providedIn: 'root'
})
export class LightsOutHubService {

  public lightOuts: Subject<LightsOuts> = new BehaviorSubject({});

  constructor() {
  }

  public createGame = async (width: number): Promise<string> => {
    return '';
  }

  public subscribeToLightsOut = (id: bigint): void => {
  }

  public unsubscribeToLightsOut = (id: bigint): void => {
  }

  private whenSubscribed = (): void => {
  }

  private whenUnsubscribed = (): void => {
  }
}
