import { inject, Injectable } from '@angular/core';
import { first, Observable, shareReplay, Subject, Subscription, timer } from "rxjs";
import { HubConnection, HubConnectionBuilder, RetryContext } from "@microsoft/signalr";
import { HubStatus, HubStatusService } from './hub-status.service';
import { base64ToNumberArray } from '../utils/base64-to-number-array';

export type CheckboxPages = { [id: string]: number[] };
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

  public subscribeToCheckboxPage = (id: string): void => {
    if (this.checkBoxPageSubscriptions[id] === undefined) {
      this.checkBoxPageSubscriptions[id] = this.createDataObservable('Checkboxes', id)
        .subscribe(items => {
          this.privateCheckboxPages[id] = items;
          this.checkboxPages.next(this.privateCheckboxPages);
        });
    }
  }

  public unsubscribeToCheckboxPage = (id: string): void => {
    if (this.checkBoxPageSubscriptions[id] === undefined) {
      return;
    }

    this.checkBoxPageSubscriptions[id].unsubscribe();
    delete this.checkBoxPageSubscriptions[id];
    delete this.privateCheckboxPages[id];
  }

  private createDataObservable = (methodName: string, id: string): Observable<number[]> => {
    return new Observable<number[]>(
      subscriber => {
        let unsubscribeData: () => Promise<void>;

        const innerSubscription = this.hubConnectionObservable
          .subscribe(async hubConnection => {
            let items: number[] = [];

            // Listen for updated data.
            hubConnection.on(`${methodName}Update`, (pageId: string, index: number, value: number) => {
              if (pageId !== id) {
                return;
              }

              items[index] = value;
              subscriber.next(items);
            });

            // Subscribe to items and process initial data.
            const result = await hubConnection.invoke(`${methodName}Subscribe`, id);
            items = base64ToNumberArray(result);
            subscriber.next(items);

            // Unsubscribe to data when subscriber leaves.
            unsubscribeData = async () => {
              await hubConnection.invoke(`${methodName}Unsubscribe`, id);
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

  setChecked(id: string, index: number, isChecked: boolean) {
    const innerSubscription = this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        // Subscribe to items and process initial data.
        const result = await hubConnection.invoke(`SetCheckbox`, id, index, isChecked ? 1 : 0);
      });
  }
}
