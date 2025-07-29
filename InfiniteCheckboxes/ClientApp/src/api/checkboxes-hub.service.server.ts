import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from "rxjs";
import { CheckboxStatistics } from './models/checkbox-statistics';
import { GlobalStatistics } from './models/global-statistics';
import { UserBalance } from './models/user-balance';

export type CheckboxPages = { [id: string]: boolean[] };
export type GoldSpots = { [id: string]: number[] };
export type CheckboxPageStatistics = { [id: string]: CheckboxStatistics };

@Injectable({
  providedIn: 'root'
})
export class CheckboxesHubService {

  public checkboxPages: Subject<CheckboxPages> = new BehaviorSubject({});
  public goldSpots: Subject<GoldSpots> = new BehaviorSubject({});
  public checkboxStatistics: Subject<CheckboxPageStatistics> = new BehaviorSubject({});
  public globalStatistics: Subject<GlobalStatistics> = new BehaviorSubject({ NumberOfChecked: 0 });
  public user: Subject<UserBalance> = new BehaviorSubject({ GoldBalance: 0 });

  public subscribeToCheckboxPage = (id: bigint, subscribeToStatistics: boolean): void => {

  }

  public unsubscribeToCheckboxPage = (id: bigint): void => {

  }

  public setChecked = async (id: bigint, index: number, isChecked: boolean): Promise<void> => {

  }

  private whenSubscribed = (): void => {

  }

  private whenUnsubscribed = (): void => {

  }
}
