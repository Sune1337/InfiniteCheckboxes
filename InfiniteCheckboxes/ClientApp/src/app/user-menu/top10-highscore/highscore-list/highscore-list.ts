import { ChangeDetectionStrategy, Component, ContentChild, effect, inject, input, OnDestroy, signal, TemplateRef } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { concatMap, map, Subject, takeUntil, timer } from 'rxjs';
import { LimitPipe } from "../../../../utils/limit-pipe";
import { Highscore } from '../../../../api/models/highscore';
import { HighscoreApiService } from '../../../../api/highscore-api.service';

@Component({
  selector: 'app-highscore-list',
  imports: [
    LimitPipe,
    NgTemplateOutlet
  ],
  templateUrl: './highscore-list.html',
  styleUrl: './highscore-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HighscoreList implements OnDestroy {

  @ContentChild('scoreTemplate') scoreTemplate?: TemplateRef<any>;

  public name = input<string>();

  protected highscore = signal<Highscore | null>(null);

  private highscoreApiservice = inject(HighscoreApiService);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    effect(() => this.getHighscores(this.name()));
  }

  ngOnDestroy(): void {
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected getHighscores = async (name: string | undefined): Promise<void> => {
    if (!name) {
      return;
    }

    const startTime = Date.now();
    this.highscoreApiservice
      .getHighscores(name)
      .pipe(takeUntil(this.ngUnsubscribe), concatMap(value =>
          timer(Math.max(0, 500 - (Date.now() - startTime))).pipe(
            map(() => value)
          )
        )
      )
      .subscribe(highscore => {
        this.highscore.set(highscore);
      });
  }

}
