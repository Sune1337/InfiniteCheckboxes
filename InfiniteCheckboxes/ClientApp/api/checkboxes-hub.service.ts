import { inject, Injectable } from '@angular/core';
import { first, Observable, ReplaySubject, shareReplay, Subject, Subscription, timer } from "rxjs";
import { HubConnection, HubConnectionBuilder, RetryContext } from "@microsoft/signalr";
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { HubStatus, HubStatusService } from './hub-status.service';
import { decompressBitArray } from '../utils/decompress';
import { bigIntToHexString, bigIntToMinimalBytes, bytesToHexString } from '../utils/bigint-utils';
import { decompressIndexAndBoolArray } from '../utils/index-and-bool-utils';
import { CheckboxStatistics } from './models/checkbox-statistics';
import { GlobalStatistics } from './models/GlobalStatistics';
import { UserBalance } from './models/user-balance';
import { createTrackedSubject } from '../utils/tracked-subject';
import { getLocalUserId } from '../src/utils/user-utils';

export type CheckboxPages = { [id: string]: boolean[] };
export type GoldSpots = { [id: string]: number[] };
export type CheckboxPageStatistics = { [id: string]: CheckboxStatistics };
type CheckboxSubscriptionParameters = { subscribeToStatistics: boolean };

@Injectable({
  providedIn: 'root'
})
export class CheckboxesHubService {

  public checkboxPages: Subject<CheckboxPages>;
  public goldSpots: Subject<GoldSpots>;
  public checkboxStatistics: Subject<CheckboxPageStatistics>;
  public globalStatistics: Subject<GlobalStatistics>;
  public user: Subject<UserBalance>;

  private subjectSubscribers = 0;
  private hubConnectionObservable: Observable<HubConnection>;
  private hubConnectionSubscription?: Subscription;
  private checkboxPageSubscriptions: { [id: string]: CheckboxSubscriptionParameters } = {};
  private privateCheckboxPages: CheckboxPages = {};
  private privateGoldSpots: GoldSpots = {};
  private privateCheckboxPageStatistics: CheckboxPageStatistics = {};

  private hubStatusService = inject(HubStatusService);

  constructor() {
    // Create observable to trigger connecting to hub.
    this.hubConnectionObservable = this.createHubConnectionObservable();

    // Create subjects.
    this.checkboxPages = createTrackedSubject(() => new Subject<CheckboxPages>(), this.whenSubscribed, this.whenUnsubscribed);
    this.goldSpots = createTrackedSubject(() => new Subject<GoldSpots>(), this.whenSubscribed, this.whenUnsubscribed);
    this.checkboxStatistics = createTrackedSubject(() => new Subject<CheckboxPageStatistics>(), this.whenSubscribed, this.whenUnsubscribed);
    this.globalStatistics = createTrackedSubject(() => new ReplaySubject<GlobalStatistics>(1), this.whenSubscribed, this.whenUnsubscribed);
    this.user = createTrackedSubject(() => new ReplaySubject<UserBalance>(1), this.whenSubscribed, this.whenUnsubscribed);
  }

  public subscribeToCheckboxPage = (id: bigint, subscribeToStatistics: boolean): void => {
    const hexId = bigIntToHexString(id);
    if (this.checkboxPageSubscriptions[hexId]) {
      return;
    }

    this.checkboxPageSubscriptions[hexId] = { subscribeToStatistics };
    const byteId = bigIntToMinimalBytes(id);

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        const compressedBytes = await hubConnection.invoke(`CheckboxesSubscribe`, byteId, subscribeToStatistics);
        if (!this.checkboxPageSubscriptions[hexId]) {
          // Caller stopped subscribing to this page before we got first data.
          return;
        }

        if (compressedBytes === null) {
          this.privateCheckboxPages[hexId] = Array(4096);
        } else {
          this.privateCheckboxPages[hexId] = decompressBitArray(compressedBytes);
        }

        this.checkboxPages.next(this.privateCheckboxPages);
      });
  }

  public unsubscribeToCheckboxPage = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (!this.checkboxPageSubscriptions[hexId]) {
      return;
    }

    const byteId = bigIntToMinimalBytes(BigInt(`0x${hexId}`));

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        await hubConnection.invoke(`CheckboxesUnsubscribe`, byteId);
      });

    delete this.checkboxPageSubscriptions[hexId];
    delete this.privateCheckboxPages[hexId];
    delete this.privateGoldSpots[hexId];
    delete this.privateCheckboxPageStatistics[hexId];
  }

  public setChecked = (id: bigint, index: number, isChecked: boolean): Promise<void> => {
    return new Promise<void>(async (resolve, reject) => {
      this.hubConnectionObservable
        .pipe(first())
        .subscribe(async hubConnection => {
          try {
            const result = await hubConnection.invoke(`SetCheckbox`, bigIntToMinimalBytes(id), index, isChecked ? 1 : 0);
            if (result) {
              reject(new Error(result));
            }
            resolve();
          } catch (error) {
            reject(error)
          }
        });
    });
  }

  private whenSubscribed = (): void => {
    this.subjectSubscribers++;

    if (this.subjectSubscribers === 1) {
      this.hubConnectionSubscription = this.hubConnectionObservable
        .subscribe(async (hubConnection): Promise<void> => {
          // Subscribe to checkbox-pages.
          for (const hexId of Object.keys(this.privateCheckboxPages)) {
            const bigIntId = BigInt(`0x${hexId}`);
            const byteId = bigIntToMinimalBytes(bigIntId);
            const compressedBytes = await hubConnection.invoke(`CheckboxesSubscribe`, byteId, this.checkboxPageSubscriptions[hexId]?.subscribeToStatistics ?? false);
            if (compressedBytes === null) {
              this.privateCheckboxPages[hexId] = Array(4096);
            } else {
              this.privateCheckboxPages[hexId] = decompressBitArray(compressedBytes);
            }

            this.checkboxPages.next(this.privateCheckboxPages);
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

    // Listen for updated checkbox-pages.
    hubConnection.on('CheckboxesUpdate', (bytePageId: Uint8Array, data: Uint8Array) => {
      const pageId = bytesToHexString(bytePageId);
      const items = this.privateCheckboxPages[pageId];
      if (!items) {
        return;
      }

      const values = decompressIndexAndBoolArray(data);
      for (const value of values) {
        items[value[0]] = value[1];
      }

      this.checkboxPages.next(this.privateCheckboxPages);
    });

    // Listen for updated gold spots.
    hubConnection.on(`GoldSpot`, (bytePageId: Uint8Array, values: number[]) => {
      const pageId = bytesToHexString(bytePageId);
      const items = this.privateGoldSpots[pageId] ?? [];

      for (const value of values) {
        items.push(value);
      }

      this.privateGoldSpots[pageId] = items;
      this.goldSpots.next(this.privateGoldSpots);
    });

    // Listen for checkbox-page statistics.
    hubConnection.on(`CS`, (bytePageId: Uint8Array, checkboxStatistics: CheckboxStatistics) => {
      const pageId = bytesToHexString(bytePageId);
      this.privateCheckboxPageStatistics[pageId] = checkboxStatistics;
      this.checkboxStatistics.next(this.privateCheckboxPageStatistics);
    });

    // Listen for global statistics.
    hubConnection.on(`GS`, (numberOfChecked: number) => {
      this.globalStatistics.next({
        NumberOfChecked: numberOfChecked
      });
    });

    // Listen for user-balance updates.
    hubConnection.on(`UB`, (userBalance: UserBalance) => {
      this.user.next(userBalance);
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
          .withUrl('/hubs/v1/CheckboxHub', { accessTokenFactory: getLocalUserId })
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
