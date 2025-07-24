import { inject, Injectable } from '@angular/core';
import { first, Observable, shareReplay, Subject, Subscription, timer } from "rxjs";
import { HubConnection, HubConnectionBuilder, RetryContext } from "@microsoft/signalr";
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { HubStatus, HubStatusService } from './hub-status.service';
import { War } from './models/war';
import { createTrackedSubject } from '../utils/tracked-subject';
import { getLocalUserId } from '../src/utils/user-utils';

export type Wars = { [id: number]: War };

@Injectable({
  providedIn: 'root'
})
export class WarHubService {

  public wars: Subject<Wars>;

  private subjectSubscribers = 0;
  private hubConnectionObservable: Observable<HubConnection>;
  private hubConnectionSubscription?: Subscription;
  private warSubscriptions: { [id: string]: boolean } = {};
  private privateWars: Wars = {};

  private hubStatusService = inject(HubStatusService);

  constructor() {
    // Create observable to trigger connecting to hub.
    this.hubConnectionObservable = this.createHubConnectionObservable();

    // Create subjects.
    this.wars = createTrackedSubject(() => new Subject<Wars>(), this.whenSubscribed, this.whenUnsubscribed);
  }

  public subscribeToWar = (id: number): void => {
    if (this.warSubscriptions[id]) {
      return;
    }

    this.warSubscriptions[id] = true;

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        const war = await hubConnection.invoke(`WarsSubscribe`, id);
        if (!this.warSubscriptions[id]) {
          // Caller stopped subscribing to this page before we got first data.
          return;
        }

        this.privateWars[id] = war;
        this.wars.next(this.privateWars);
      });
  }

  public unsubscribeToWar = (id: number): void => {
    if (!this.warSubscriptions[id]) {
      return;
    }

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        await hubConnection.invoke(`WarsUnsubscribe`, id);
      });

    delete this.warSubscriptions[id];
    delete this.privateWars[id];
  }

  public getCurrentWar = (): Promise<number> => {
    return new Promise<number>(async (resolve, reject) => {
      this.hubConnectionObservable
        .pipe(first())
        .subscribe(async hubConnection => {
          try {
            const warGameId = await hubConnection.invoke<number>(`GetCurrentWarId`);
            resolve(warGameId);
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
          // Subscribe to wars.
          for (const idString of Object.keys(this.privateWars)) {
            const id = parseInt(idString);
            const war = await hubConnection.invoke(`WarsSubscribe`, id);
            if (!this.warSubscriptions[id]) {
              // Caller stopped subscribing to this page before we got first data.
              return;
            }

            this.privateWars[id] = war;
            this.wars.next(this.privateWars);
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
    hubConnection.on(`WarsUpdate`, (warId: number, war: War) => {
      if (!this.privateWars[warId]) {
        return;
      }

      this.privateWars[warId] = war;
      this.wars.next(this.privateWars);
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
          .withUrl('/hubs/v1/WarHub', { accessTokenFactory: getLocalUserId })
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
