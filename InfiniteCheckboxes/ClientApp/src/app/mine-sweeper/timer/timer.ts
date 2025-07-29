import { ChangeDetectionStrategy, Component, effect, input, OnDestroy, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Subject, Subscription, takeUntil, timer } from 'rxjs';

@Component({
  selector: 'app-timer',
  imports: [
    DecimalPipe
  ],
  templateUrl: './timer.html',
  styleUrl: './timer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Timer implements OnDestroy {

  public startDate = input<Date>();
  public endDate = input<Date>();

  protected elapsedHours = signal(0);
  protected elapsedMinutes = signal(0);
  protected elapsedSeconds = signal(0);

  private timerSubscription?: Subscription;

  private ngUnsubscribe = new Subject<void>();

  constructor() {
    effect(() => this.processStartStop(this.startDate(), this.endDate()));
  }

  protected processStartStop = (startDate?: Date, endDate?: Date): void => {
    if (!!startDate == !!endDate) {
      // Timer is not started or it is stopped.
      if (this.timerSubscription) {
        this.timerSubscription.unsubscribe();
        this.timerSubscription = undefined;
      }

      if (startDate && endDate) {
        // Tick timer once to output the elapsed time.
        this.tickTimer();
      }

      return;
    }

    if (startDate && !endDate) {
      // Timer started.
      this.timerSubscription = timer(0, 1000)
        .pipe(takeUntil(this.ngUnsubscribe))
        .subscribe(time => this.tickTimer());
    }
  }

  ngOnDestroy(): void {
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  private tickTimer = () => {
    const startDate = this.startDate();
    if (!startDate) {
      return;
    }

    const endDate = this.endDate() ?? new Date();
    let elapsedMs = endDate.getTime() - startDate.getTime();

    this.elapsedHours.set(Math.floor(elapsedMs / 3600000));
    elapsedMs -= this.elapsedHours() * 3600000;
    this.elapsedMinutes.set(Math.floor(elapsedMs / 60000));
    elapsedMs -= this.elapsedMinutes() * 60000;
    this.elapsedSeconds.set(Math.floor(elapsedMs / 1000));
  }
}
