import { AfterViewInit, Component, computed, effect, inject, OnDestroy, OnInit, signal, TemplateRef, ViewChild } from '@angular/core';
import { HeaderService } from '../../utils/header.service';
import { Meta, Title } from '@angular/platform-browser';
import { combineLatest, filter, Subject, takeUntil } from 'rxjs';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MinesweeperHubService } from '../../api/minesweeper-hub.service';
import { getErrorMessage } from '../../utils/get-error-message';
import { Minesweeper } from '../../api/models/minesweeper';
import { CheckboxGrid } from '../checkbox-grid/checkbox-grid';
import { CheckboxesHubService } from '../../api/checkboxes-hub.service';
import { AsyncPipe } from '@angular/common';
import { bigIntToHexString } from '../../utils/bigint-utils';

@Component({
  selector: 'app-mine-sweeper',
  imports: [
    FormsModule,
    CheckboxGrid,
    AsyncPipe
  ],
  templateUrl: './mine-sweeper.html',
  styleUrl: './mine-sweeper.scss'
})
export class MinesweeperComponent implements OnInit, AfterViewInit, OnDestroy {

  @ViewChild('headerTemplate')
  private headerTemplate!: TemplateRef<unknown>;

  protected widthOptions = [8, 12, 16, 24, 32, 48, 64];
  protected selectedWidth = signal<any>(this.widthOptions[0]);
  protected minesOptions = computed(() => this.createNumberOfMinesOptions(this.selectedWidth()))
  protected selectedNumberOfMines = signal<any>(this.widthOptions[0]);
  protected minesweeper = signal<Minesweeper | null>(null);
  protected flagPage = signal<boolean[] | null>(null);
  protected checkboxStyles = new Subject<(string | null)[]>();

  private bigintZero = BigInt(0);
  private currentMinesweeperId = signal(BigInt(0));
  private currentFlagPageId = signal(BigInt(0));
  private flags = new Subject<boolean[]>();
  private mines = new Subject<number[]>();
  private counts = new Subject<{ [id: number]: number }>();

  private headerService = inject(HeaderService);
  private title = inject(Title);
  private meta = inject(Meta);
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private minesweeperHubService = inject(MinesweeperHubService);
  private checkboxesHubService = inject(CheckboxesHubService);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    this.title.setTitle('Minesweeper');
    this.meta.updateTag({ name: 'description', content: 'Play the classic Minesweeper but with checkboxes.' });

    // Register callback to handle updates to minesweeper.
    this.minesweeperHubService.minesweepers
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(minesweepers => {
        const hexId = bigIntToHexString(this.currentMinesweeperId());
        if (minesweepers[hexId]) {
          const minesweeper = minesweepers[hexId];
          this.minesweeper.set(minesweeper);
          this.mines.next(minesweeper.Mines ?? []);
          this.updateSubscriptions(this.currentMinesweeperId(), BigInt(`0x${minesweeper.FlagLocationId}`))
        }
      });

    // Register callback to handle updates to counts.
    this.minesweeperHubService.minesweeperCounts
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(minesweeperCounts => {
        const hexId = bigIntToHexString(this.currentMinesweeperId());
        if (minesweeperCounts[hexId]) {
          this.counts.next(minesweeperCounts[hexId]);
        }
      });

    // Register callback to handle updates to checkbox pages.
    this.checkboxesHubService.checkboxPages
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(checkboxPages => {
        const hexId = bigIntToHexString(this.currentFlagPageId());
        const flagPage = this.flagPage();
        if (!flagPage || (checkboxPages[hexId] && !checkboxPages[hexId].every((element, index) => element === flagPage[index]))) {
          this.flagPage.set([...checkboxPages[hexId]]);
          this.flags.next(checkboxPages[hexId]);
        }
      });

    // Merge styles from flags and mines.
    combineLatest([this.flags, this.mines, this.counts])
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(([flags, mines, counts]) => {
        const result: (string | null)[] = [];
        const maxLength = Math.max(flags.length, mines.length, Object.keys(counts).length);
        for (let i = 0; i < maxLength; i++) {
          if (mines.indexOf(i) >= 0) {
            result.push('mine');
          } else if (counts[i]) {
            result.push(`count-${counts[i]}`);
          } else if (flags[i]) {
            result.push('flag');
          } else {
            result.push(null);
          }
        }
        this.checkboxStyles.next(result);
      });

    effect(() => {
      // When game-size changes, select a default number of mines.
      const selectedNumberOfMines = parseInt(this.selectedNumberOfMines());
      const selectedWidth = parseInt(this.selectedWidth());
      if (selectedNumberOfMines < selectedWidth) {
        this.selectedNumberOfMines.set(selectedWidth);
      } else if (selectedNumberOfMines > selectedWidth * selectedWidth / 2) {
        this.selectedNumberOfMines.set(selectedWidth);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntil(this.ngUnsubscribe)
      )
      .subscribe(() => {
        const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
        this.updateSubscriptions(id ? BigInt(`0x${id}`) : this.bigintZero, this.bigintZero);
      });

    const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
    if (id) {
      this.updateSubscriptions(id ? BigInt(`0x${id}`) : this.bigintZero, this.bigintZero);
    }
  }

  async ngAfterViewInit(): Promise<void> {
    // Set header template.
    this.headerService.setHeader(this.headerTemplate);
  }

  ngOnDestroy() {
    this.flags.complete();
    this.mines.complete();
    this.updateSubscriptions(this.bigintZero, this.bigintZero);
    this.headerService.setHeader(null);
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected whenDialogClick = (event: MouseEvent, helpDialog: HTMLDialogElement): void => {
    if (event.target === helpDialog) {
      helpDialog.close();
    }
  }

  protected whenStartClick = async (): Promise<void> => {
    try {
      const minesweeperId = await this.minesweeperHubService.createGame(parseInt(this.selectedWidth()), parseInt(this.selectedNumberOfMines()));
      this.router.navigate(['Minesweeper', minesweeperId]);
    } catch (error: any) {
      alert(getErrorMessage(error));
    }
  }

  protected whenRightMouseButtonClick = async (index: number): Promise<void> => {
    const flags = this.flagPage();
    if (!flags) {
      return;
    }

    const flagged = !flags[index];
    try {
      this.setFlag(index, flagged);
      await this.checkboxesHubService.setChecked(this.currentFlagPageId(), index, flagged);
    } catch (error) {
      this.setFlag(index, !flagged);
      alert(getErrorMessage(error));
    }
  }

  private setFlag = (index: number, value: boolean): void => {
    const flagPage = this.flagPage() ?? new Array(4096);
    flagPage[index] = value;
    this.flags.next(flagPage);
  }

  private updateSubscriptions = (id: bigint, flagLocationId: bigint): void => {
    // Update minesweeper subscription.
    const currentMineSweeperId = this.currentMinesweeperId();
    if (currentMineSweeperId && currentMineSweeperId !== id) {
      this.minesweeperHubService.unsubscribeToMinesweeper(currentMineSweeperId);
      this.currentMinesweeperId.set(this.bigintZero);
      this.minesweeper.set(null);
      this.counts.next({});
    }

    if (id && currentMineSweeperId !== id) {
      this.currentMinesweeperId.set(id);
      this.minesweeperHubService.subscribeToMinesweeper(id);
    }

    // Update flag-location subscription.
    const currentFlagLocationId = this.currentFlagPageId();
    if (currentFlagLocationId && currentFlagLocationId != flagLocationId) {
      this.checkboxesHubService.unsubscribeToCheckboxPage(currentFlagLocationId);
      this.currentFlagPageId.set(this.bigintZero);
      this.flagPage.set(null);
    }

    if (flagLocationId && currentFlagLocationId !== flagLocationId) {
      this.currentFlagPageId.set(flagLocationId);
      this.checkboxesHubService.subscribeToCheckboxPage(flagLocationId, false);
    }
  }

  private createNumberOfMinesOptions(width: number): number[] {
    const end = (width * width) * 0.25;
    const result: number[] = [];
    for (let i = width; i <= end; i++) {
      result.push(i);
    }
    return result;
  }
}
