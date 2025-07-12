import { inject, Injectable } from '@angular/core';
import { first, Observable, shareReplay, Subject, Subscription, timer } from "rxjs";
import { HubConnection, HubConnectionBuilder, RetryContext } from "@microsoft/signalr";
import { HubStatus, HubStatusService } from './hub-status.service';
import { base64ToUint8Array, decompressBitArray } from '../utils/decompress';
import { base64ToBigInt, bigIntToBase64, bigIntToHexString } from '../utils/bigint-utils';

export type CheckboxPages = { [id: string]: boolean[] };
type CheckboxPageSubscriptions = { [id: string]: Subscription };

@Injectable({
  providedIn: 'root'
})
export class CheckboxesHubService {

  public checkboxPages: Subject<CheckboxPages>;

  private hubStatusService = inject(HubStatusService);
  private hubConnectionObservable: Observable<HubConnection>;
  private checkBoxPageSubscriptions: CheckboxPageSubscriptions = {};
  private privateCheckboxPages: CheckboxPages = {};

  constructor() {
    // Create observable to trigger connecting to hub.
    this.hubConnectionObservable = this.createHubConnectionObservable();

    // Create machineSimulations observable.
    this.checkboxPages = new Subject<CheckboxPages>();
  }

  public subscribeToCheckboxPage = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (this.checkBoxPageSubscriptions[hexId] === undefined) {
      this.checkBoxPageSubscriptions[hexId] = this.createDataObservable('Checkboxes', id)
        .subscribe(items => {
          this.privateCheckboxPages[hexId] = items;
          this.checkboxPages.next(this.privateCheckboxPages);
        });
    }
  }

  public unsubscribeToCheckboxPage = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (this.checkBoxPageSubscriptions[hexId] === undefined) {
      return;
    }

    this.checkBoxPageSubscriptions[hexId].unsubscribe();
    delete this.checkBoxPageSubscriptions[hexId];
    delete this.privateCheckboxPages[hexId];
  }

  private createDataObservable = (methodName: string, id: bigint): Observable<boolean[]> => {
    return new Observable<boolean[]>(
      subscriber => {
        let unsubscribeData: () => Promise<void>;

        const innerSubscription = this.hubConnectionObservable
          .subscribe(async hubConnection => {
            const base64Id = bigIntToBase64(id);
            let items: boolean[] = [];

            // Listen for updated data.
            hubConnection.on(`${methodName}Update`, (base64PageId: string, values: number[][]) => {
              const pageId = base64ToBigInt(base64PageId);
              if (pageId !== id) {
                return;
              }

              for (const value of values) {
                items[value[0]] = value[1] != 0;
              }

              subscriber.next(items);
            });

            // Subscribe to items and process initial data.
            const base64Data = await hubConnection.invoke(`${methodName}Subscribe`, base64Id);
            if (base64Data === null) {
              items = Array(4096);
            } else {
              const compressedBytes = base64ToUint8Array(base64Data);
              items = decompressBitArray(compressedBytes);
            }
            subscriber.next(items);

            // Unsubscribe to data when subscriber leaves.
            unsubscribeData = async () => {
              await hubConnection.invoke(`${methodName}Unsubscribe`, base64Id);
            }
          });

        const unsubscribe = async () => {
          if (unsubscribeData !== undefined) {
            await unsubscribeData();
          }

          innerSubscription.unsubscribe();
        };

        return () => {
          unsubscribe();
        }
      }
    )
      .pipe(shareReplay({ bufferSize: 1, refCount: true }));
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
          .withUrl('/hubs/v1/CheckboxHub')
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

  setChecked(id: bigint, index: number, isChecked: boolean) {
    const innerSubscription = this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        // Subscribe to items and process initial data.
        await hubConnection.invoke(`SetCheckbox`, bigIntToBase64(id), index, isChecked ? 1 : 0);
      });
  }
}
