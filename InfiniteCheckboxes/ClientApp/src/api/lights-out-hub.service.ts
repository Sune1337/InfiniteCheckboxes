import { inject, Injectable } from '@angular/core';
import { first, Observable, shareReplay, Subject, Subscription, timer } from "rxjs";
import { HubConnection, HubConnectionBuilder, RetryContext } from "@microsoft/signalr";
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { HubStatus, HubStatusService } from './hub-status.service';
import { createTrackedSubject } from '../utils/tracked-subject';
import { getLocalUserId } from '#userUtils';
import { LightsOut } from './models/lights-out';
import { bigIntToHexString, bigIntToMinimalBytes, bytesToHexString } from '../utils/bigint-utils';

export type LightsOuts = { [id: string]: LightsOut };

@Injectable({
  providedIn: 'root'
})
export class LightsOutHubService {

  public lightOuts: Subject<LightsOuts>;

  private subjectSubscribers = 0;
  private hubConnectionObservable: Observable<HubConnection>;
  private hubConnectionSubscription?: Subscription;
  private lightsOutSubscription: { [id: string]: boolean } = {};
  private privateLightsOut: LightsOuts = {};

  private hubStatusService = inject(HubStatusService);

  constructor() {
    // Create observable to trigger connecting to hub.
    this.hubConnectionObservable = this.createHubConnectionObservable();

    // Create subjects.
    this.lightOuts = createTrackedSubject(() => new Subject<LightsOuts>(), this.whenSubscribed, this.whenUnsubscribed);
  }

  public createGame = async (width: number): Promise<string> => {
    return new Promise<string>(async (resolve, reject) => {
      this.hubConnectionObservable
        .pipe(first())
        .subscribe(async hubConnection => {
          try {
            const lightsOutId = await hubConnection.invoke<string>('CreateGame', width);
            if (!lightsOutId) {
              reject(new Error('Could not create game.'));
            }

            resolve(lightsOutId);
          } catch (error) {
            reject(error)
          }
        });
    });
  }

  public subscribeToLightsOut = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (this.lightsOutSubscription[hexId]) {
      return;
    }

    this.lightsOutSubscription[hexId] = true;

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        const lightsOut = await hubConnection.invoke(`LightsOutSubscribe`, bigIntToMinimalBytes(id));
        if (!this.lightsOutSubscription[hexId]) {
          // Caller stopped subscribing to this page before we got first data.
          return;
        }

        this.privateLightsOut[hexId] = lightsOut;
        this.lightOuts.next(this.privateLightsOut);
      });
  }

  public unsubscribeToLightsOut = (id: bigint): void => {
    const hexId = bigIntToHexString(id);
    if (!this.lightsOutSubscription[hexId]) {
      return;
    }

    this.hubConnectionObservable
      .pipe(first())
      .subscribe(async hubConnection => {
        await hubConnection.invoke(`LightsOutUnsubscribe`, bigIntToMinimalBytes(id));
      });

    delete this.lightsOutSubscription[hexId];
    delete this.privateLightsOut[hexId];
  }

  private whenSubscribed = (): void => {
    this.subjectSubscribers++;

    if (this.subjectSubscribers === 1) {
      this.hubConnectionSubscription = this.hubConnectionObservable
        .subscribe(async (hubConnection): Promise<void> => {
          // Subscribe to lights-out.
          for (const hexId of Object.keys(this.privateLightsOut)) {
            const bigIntId = BigInt(`0x${hexId}`);
            const byteId = bigIntToMinimalBytes(bigIntId);
            const lightsOut = await hubConnection.invoke(`LightsOutSubscribe`, byteId);
            if (!this.lightsOutSubscription[hexId]) {
              // Caller stopped subscribing to this page before we got first data.
              return;
            }

            this.privateLightsOut[hexId] = lightsOut;
            this.lightOuts.next(this.privateLightsOut);
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
    hubConnection.on(`LightsOutUpdate`, (byteLightsOutId: Uint8Array, lightsOut: LightsOut) => {
      const lightsOutId = bytesToHexString(byteLightsOutId);
      if (!this.privateLightsOut[lightsOutId]) {
        return;
      }

      this.privateLightsOut[lightsOutId] = lightsOut;
      this.lightOuts.next(this.privateLightsOut);
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
          .withUrl('/hubs/v1/LightsOutHub', { accessTokenFactory: getLocalUserId })
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
