import { Injectable } from '@angular/core';
import { BehaviorSubject, Subject } from "rxjs";
import { Minesweeper } from './models/minesweeper';

export type Minesweepers = { [id: string]: Minesweeper };
export type MinesweeperCounts = { [id: string]: { [id: number]: number } };

@Injectable({
  providedIn: 'root'
})
export class MinesweeperHubService {

  public minesweepers: Subject<Minesweepers> = new BehaviorSubject({});
  public minesweeperCounts: Subject<MinesweeperCounts> = new BehaviorSubject({});

  constructor() {
  }

  public createGame = async (width: number, numberOfMines: number, luckyStart: boolean): Promise<string> => {
    return '';
  }

  public subscribeToMinesweeper = (id: bigint): void => {

  }

  public unsubscribeToMinesweeper = (id: bigint): void => {

  }

  private whenSubscribed = (): void => {

  }

  private whenUnsubscribed = (): void => {

  }
}
