import { AfterViewInit, ChangeDetectionStrategy, Component, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule } from "@angular/forms";
import { Subject, takeUntil } from 'rxjs';
import { WarHubService, Wars } from '../../../api/war-hub.service';
import { War } from '../../../api/models/war';
import { CheckboxesHubService, CheckboxPageStatistics } from '../../../api/checkboxes-hub.service';
import { CheckboxGrid } from "../checkbox-grid/checkbox-grid";

@Component({
  selector: 'app-war',
  imports: [
    CheckboxGrid,
    ReactiveFormsModule,
    DatePipe
  ],
  templateUrl: './war.html',
  styleUrl: './war.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WarComponent implements AfterViewInit, OnDestroy {

  protected war = signal<War | null>(null);
  protected numberOfPlayers = signal(0);

  @ViewChild(CheckboxGrid)
  private checkboxGrid!: CheckboxGrid;

  private currentWarId = -1;

  private warHubService = inject(WarHubService);
  private checkboxHubService = inject(CheckboxesHubService);
  private activatedRoute = inject(ActivatedRoute);
  private router = inject(Router);
  private title = inject(Title);
  private meta = inject(Meta);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    this.title.setTitle('Checkbox war');
    this.meta.updateTag({ name: 'description', content: 'Play the checkbox war in real-time with warriors from all over the world. The goal is to check or uncheck all the checkboxes.' });

    // Register callback to handle updates to checkbox-pages.
    this.warHubService.wars
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(wars => this.warUpdated(wars));

    // Register callback to handle checkbox-statistics updates.
    this.checkboxHubService.checkboxStatistics
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(checkboxStatistics => {
        this.checkboxStatisticsUpdated(checkboxStatistics);
      });
  }

  async ngAfterViewInit(): Promise<void> {
    this.activatedRoute.paramMap.subscribe(async params => {
      const id = params.get('id');
      if (id) {
        if (this.currentWarId >= 0) {
          this.warHubService.unsubscribeToWar(this.currentWarId);
        }

        this.currentWarId = parseInt(id);
        if (this.currentWarId >= 0) {
          this.warHubService.subscribeToWar(this.currentWarId);
        }
      } else {
        // Load current war.
        const currentWarId = await this.warHubService.getCurrentWar();
        this.router.navigate(['War', currentWarId], { replaceUrl: true });
      }
    });
  }

  ngOnDestroy() {
    if (this.currentWarId >= 0) {
      this.warHubService.unsubscribeToWar(this.currentWarId);
    }

    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected whenDialogClick = (event: MouseEvent, helpDialog: HTMLDialogElement): void => {
    if (event.target === helpDialog) {
      helpDialog.close();
    }
  }

  protected gotoCurrentWar = (): void => {
    this.router.navigate(['War']);
  }

  private warUpdated = (wars: Wars): void => {
    const updatedWar = wars[this.currentWarId];
    if (!updatedWar || !updatedWar.warLocationId) {
      return;
    }

    if (this.war()?.warLocationId !== updatedWar.warLocationId) {
      if (!this.checkboxGrid) {
        setTimeout(() => {
          this.checkboxGrid.navigateToPage(`0x${updatedWar.warLocationId}`);
        });
      } else {
        this.checkboxGrid.navigateToPage(`0x${updatedWar.warLocationId}`);
      }
    }

    this.war.set(updatedWar);
  }

  private checkboxStatisticsUpdated = (checkboxStatistics: CheckboxPageStatistics): void => {
    const war = this.war();
    if (!war?.warLocationId) {
      return;
    }

    const stats = checkboxStatistics[war.warLocationId];
    if (!stats){
      return;
    }

    this.numberOfPlayers.set(stats.numberOfSubscribers);
  }
}
