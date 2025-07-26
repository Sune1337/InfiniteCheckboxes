import { ChangeDetectionStrategy, Component, inject, OnDestroy, signal } from '@angular/core';
import { Subject, Subscription, takeUntil } from 'rxjs';
import { Tab } from './tabs/tab/tab';
import { Tabs } from './tabs/tabs';
import { HighscoreApiService } from '../../../api/highscore-api.service';
import { Highscore } from '../../../api/models/highscore';
import { LimitPipe } from '../../../utils/limit-pipe';

@Component({
  selector: 'app-top10-highscore',
  imports: [
    Tab,
    Tabs,
    LimitPipe
  ],
  templateUrl: './top10-highscore.html',
  styleUrl: './top10-highscore.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Top10Highscore implements OnDestroy {

  protected checkersHighscore = signal<Highscore | null>(null);
  protected uncheckersHighscore = signal<Highscore | null>(null);
  protected goldDiggersHighscore = signal<Highscore | null>(null);
  protected minesweeperHighscore = signal<Highscore | null>(null);

  private getHighscoresSubscription?: Subscription;

  private highscoreApiservice = inject(HighscoreApiService);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    this.getHighscores();
  }

  ngOnDestroy(): void {
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected getHighscores = async (): Promise<void> => {
    this.getHighscoresSubscription = this.highscoreApiservice
      .getHighscores()
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(highscores => {
        this.getHighscoresSubscription = undefined;

        for (const highscore of highscores) {
          switch (highscore.name) {
            case "Checked":
              this.checkersHighscore.set(highscore);
              break;

            case "Unchecked":
              this.uncheckersHighscore.set(highscore);
              break;

            case "GoldDigger":
              this.goldDiggersHighscore.set(highscore);
              break;

            case "Minesweeper":
              this.minesweeperHighscore.set(highscore);
              break;
          }
        }
      });
  }
}
