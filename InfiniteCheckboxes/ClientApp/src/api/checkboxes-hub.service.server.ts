import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from "rxjs";
import { CheckboxStatistics } from './models/checkbox-statistics';
import { GlobalStatistics } from './models/global-statistics';
import { UserBalance } from './models/user-balance';

export type CheckboxPage = { id: string, state: boolean[] };
export type PageGoldSpots = { id: string, state: number[] };
export type CheckboxPageStatistics = { [id: string]: CheckboxStatistics };

@Injectable({
  providedIn: 'root'
})
export class CheckboxesHubService {

  public checkboxPages: Subject<CheckboxPage> = new BehaviorSubject({ id: '0', state: Array(4096) });
  public goldSpots: Subject<PageGoldSpots> = new BehaviorSubject({ 'id': '0', state: Array(0) });
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
