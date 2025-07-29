import { inject, Injectable } from '@angular/core';
import { first, Observable, shareReplay, Subject, Subscription, timer } from "rxjs";
import { HubConnection, HubConnectionBuilder, RetryContext } from "@microsoft/signalr";
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { HubStatus, HubStatusService } from './hub-status.service';
import { createTrackedSubject } from '../utils/tracked-subject';
import { getLocalUserId } from '#userUtils';
import { Minesweeper } from './models/minesweeper';
import { bigIntToHexString, bigIntToMinimalBytes, bytesToHexString } from '../utils/bigint-utils';

export type Minesweepers = { [id: string]: Minesweeper };
export type MinesweeperCounts = { [id: string]: { [id: number]: number } };

@Injectable({
  providedIn: 'root'
})
export class MinesweeperHubService {

  public minesweepers: Subject<Minesweepers>;
  public minesweeperCounts: Subject<MinesweeperCounts>;

  private subjectSubscribers = 0;
  private hubConnectionObservable: Observable<HubConnection>;
  private hubConnectionSubscription?: Subscription;
  private minesweeperSubscriptions: { [id: string]: boolean } = {};
  private privateMinesweepers: Minesweepers = {};
  private privateMinesweeperCounts: MinesweeperCounts = {};

  private hubStatusService = inject(HubStatusService);

  constructor() {
    // Create observable to trigger connecting to hub.
    this.hubConnectionObservable = this.createHubConnectionObservable();

    // Create subjects.
    this.minesweepers = createTrackedSubject(() => new Subject<Minesweepers>(), this.whenSubscribed, this.whenUnsubscribed);
    this.minesweeperCounts = createTrackedSubject(() => new Subject<MinesweeperCounts>(), this.whenSubscribed, this.whenUnsubscribed);
  }

  public createGame = async (width: number, numberOfMines: number, luckyStart: boolean): Promise<string> => {
    return new Promise<string>(async (resolve, reject) => {
      this.hubConnectionObservable
        .pipe(first())
        .subscribe(async hubConnection => {
          try {
            const minesweeperId = await hubConnection.invoke<string>('CreateGame', width, numberOfMines, luckyStart);
            if (!minesweeperId) {
              reject(new Error('Could not create game.'));
            }

            resolve(minesweeperId);
          } catch (error) {
            reject(error)
          }
        });
    });
  }

  public subscribeToMinesweeper = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (this.minesweeperSubscriptions[hexId]) {
      return;
    }

    this.minesweeperSubscriptions[hexId] = true;
    this.privateMinesweeperCounts[hexId] = [];

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        const minesweeper = await hubConnection.invoke(`MinesweeperSubscribe`, bigIntToMinimalBytes(id));
        if (!this.minesweeperSubscriptions[hexId]) {
          // Caller stopped subscribing to this page before we got first data.
          return;
        }

        this.privateMinesweepers[hexId] = minesweeper;
        this.minesweepers.next(this.privateMinesweepers);

        if (minesweeper.MineCounts) {
          this.privateMinesweeperCounts[hexId] = minesweeper.MineCounts;
          this.minesweeperCounts.next(this.privateMinesweeperCounts);
        }
      });
  }

  public unsubscribeToMinesweeper = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (!this.minesweeperSubscriptions[hexId]) {
      return;
    }

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        await hubConnection.invoke(`MinesweeperUnsubscribe`, bigIntToMinimalBytes(id));
      });

    delete this.minesweeperSubscriptions[hexId];
    delete this.privateMinesweepers[hexId];
    delete this.privateMinesweeperCounts[hexId];
  }

  private whenSubscribed = (): void => {
    this.subjectSubscribers++;

    if (this.subjectSubscribers === 1) {
      this.hubConnectionSubscription = this.hubConnectionObservable
        .subscribe(async (hubConnection): Promise<void> => {
          // Subscribe to mine sweeper.
          for (const hexId of Object.keys(this.privateMinesweepers)) {
            const bigIntId = BigInt(`0x${hexId}`);
            const byteId = bigIntToMinimalBytes(bigIntId);
            const minesweeper = await hubConnection.invoke(`MinesweeperSubscribe`, byteId);
            if (!this.minesweeperSubscriptions[hexId]) {
              // Caller stopped subscribing to this page before we got first data.
              return;
            }

            this.privateMinesweepers[hexId] = minesweeper;
            this.minesweepers.next(this.privateMinesweepers);
          }
        });
    }
  }

  private whenUnsubscribed = (): void => {
    this.subjectSubscribers--;

    if (this.subjectSubscribers === 0 && this.hubConnectionSubscription) {
      this.hubConnectionSubscription.unsubscribe();
      this.hubConnectionSubscription = undefined;
    }
  }

  private beforeHubStart = (hubConnection: HubConnection): void => {
    // This is the first connection to the hub. Register callbacks.

    // Listen for updated data.
    hubConnection.on(`MinesweeperUpdate`, (byteMinesweeperId: Uint8Array, minesweeper: Minesweeper) => {
      const minesweeperId = bytesToHexString(byteMinesweeperId);
      if (!this.privateMinesweepers[minesweeperId]) {
        return;
      }

      this.privateMinesweepers[minesweeperId] = minesweeper;
      this.minesweepers.next(this.privateMinesweepers);
    });

    hubConnection.on(`MinesweeperCounts`, (byteMinesweeperId: Uint8Array, counts: { [id: number]: number }) => {
      const minesweeperId = bytesToHexString(byteMinesweeperId);
      if (!this.privateMinesweepers[minesweeperId]) {
        return;
      }

      const currentCounts = this.privateMinesweeperCounts[minesweeperId];
      for (const key of Object.keys(counts)) {
        const index = parseInt(key);
        currentCounts[index] = counts[index];
      }

      this.privateMinesweeperCounts[minesweeperId] = currentCounts;
      this.minesweeperCounts.next(this.privateMinesweeperCounts);
    });
  }

  /**
   * Create an observable that will emit the hubConnection it is started or reconnected.
   * When it is emitted, the subscribers should subscribe to data and register event-handlers.
   */
  private createHubConnectionObservable = (): Observable<HubConnection> => {
    const hubStatusService = this.hubStatusService;

    return new Observable<HubConnection>(
      subscriber => {
        let unsubscribed = false;

        // Set initial hub-status.
        hubStatusService.SetStatus(HubStatus.Connecting);

        // Connect to hub.
        const hubConnection = new HubConnectionBuilder()
          .withUrl('/hubs/v1/MinesweeperHub', { accessTokenFactory: getLocalUserId })
          .withHubProtocol(new MessagePackHubProtocol())
          .withAutomaticReconnect({
            // Retry connecting to hub until the observable is unsubscribed.
            nextRetryDelayInMilliseconds(retryContext: RetryContext): number | null {
              if (retryContext.previousRetryCount === 0) {
                // Retry connecting immediately on first try.
                return 0;
              }

              if (unsubscribed) {
                return null;
              }

              return 8000 + Math.random() * 4000;
            }
          })
          .build();

        hubConnection.onreconnected(() => {
          subscriber.next(hubConnection);
          hubStatusService.SetStatus(HubStatus.Connected);
        })

        hubConnection.onreconnecting(() => {
          hubStatusService.SetStatus(HubStatus.Connecting);
        });

        // Start the connection.
        let cancelRetryStart: (() => void) | undefined = undefined;

        async function startHubConnection() {
          while (!unsubscribed) {
            try {
              await hubConnection.start();
              hubStatusService.SetStatus(HubStatus.Connected);
              subscriber.next(hubConnection);
              break;
            } catch (e) {
              await new Promise<void>((res, err) => {
                const timeout = setTimeout(() => res(), 5000 + Math.random() * 10000);
                cancelRetryStart = () => {
                  clearTimeout(timeout);
                  res();
                };
              });
              cancelRetryStart = undefined;
            }
          }
        }

        this.beforeHubStart(hubConnection);
        startHubConnection();

        return () => {
          unsubscribed = true;
          cancelRetryStart?.();
          hubConnection.stop();
          hubStatusService.SetStatus(HubStatus.Idle);
        };
      }
    )
      // Delay closing the subscription when last subscriber leaves so that the hub is not reconnected is user navigates to another page that also uses the hub.
      // @ts-ignore
      .pipe(shareReplay({ bufferSize: 1, refCount: () => timer(1000) }));
  }
}
