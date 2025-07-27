import { AfterViewInit, ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal, TemplateRef, ViewChild } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ReactiveFormsModule } from "@angular/forms";
import { filter, Subject, takeUntil } from 'rxjs';
import { WarHubService, Wars } from '../../api/war-hub.service';
import { War } from '../../api/models/war';
import { CheckboxesHubService, CheckboxPageStatistics } from '../../api/checkboxes-hub.service';
import { CheckboxGrid } from "../checkbox-grid/checkbox-grid";
import { HeaderService } from '../../utils/header.service';

@Component({
  selector: 'app-war',
  imports: [
    CheckboxGrid,
    ReactiveFormsModule
  ],
  templateUrl: './war.html',
  styleUrl: './war.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WarComponent implements OnInit, AfterViewInit, OnDestroy {

  protected war = signal<War | null>(null);
  protected numberOfPlayers = signal(0);

  @ViewChild(CheckboxGrid)
  private checkboxGrid!: CheckboxGrid;
  @ViewChild('headerTemplate')
  private headerTemplate!: TemplateRef<unknown>;

  private currentWarId = -1;

  private headerService = inject(HeaderService);
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

    // Register callback to handle updates to wars.
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

  async ngOnInit(): Promise<void> {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntil(this.ngUnsubscribe)
      )
      .subscribe(() => {
        const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
        if (id) {
          this.updateSubscriptions(parseInt(id));
        }
      });

    const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
    if (id) {
      this.updateSubscriptions(parseInt(id));
    } else {
      // Load current war.
      const currentWarId = await this.warHubService.getCurrentWar();
      this.router.navigate(['War', currentWarId], { replaceUrl: true });
    }
  }

  async ngAfterViewInit(): Promise<void> {
    // Set header template.
    this.headerService.setHeader(this.headerTemplate);
  }

  ngOnDestroy() {
    this.headerService.setHeader(null);

    this.updateSubscriptions(-1);
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected whenDialogClick = (event: MouseEvent, helpDialog: HTMLDialogElement): void => {
    if (event.target === helpDialog) {
      helpDialog.close();
    }
  }

  protected gotoCurrentWar = async (): Promise<void> => {
    // Load current war.
    const currentWarId = await this.warHubService.getCurrentWar();
    this.router.navigate(['War', currentWarId], { replaceUrl: true });
  }

  private warUpdated = (wars: Wars): void => {
    const updatedWar = wars[this.currentWarId];
    if (!updatedWar || !updatedWar.WarLocationId) {
      return;
    }

    if (this.war()?.WarLocationId !== updatedWar.WarLocationId) {
      if (!this.checkboxGrid) {
        setTimeout(() => {
          this.checkboxGrid.navigateToPage(`0x${updatedWar.WarLocationId}`);
        });
      } else {
        this.checkboxGrid.navigateToPage(`0x${updatedWar.WarLocationId}`);
      }
    }

    this.war.set(updatedWar);
  }

  private checkboxStatisticsUpdated = (checkboxStatistics: CheckboxPageStatistics): void => {
    const war = this.war();
    if (!war?.WarLocationId) {
      return;
    }

    const stats = checkboxStatistics[war.WarLocationId];
    if (!stats) {
      return;
    }

    this.numberOfPlayers.set(stats.NumberOfSubscribers);
  }

  private updateSubscriptions = (warId: number): void => {
    // Update minesweeper subscription.
    const currentWarId = this.currentWarId;
    if (currentWarId >= 0 && currentWarId !== warId) {
      this.warHubService.unsubscribeToWar(currentWarId);
      this.currentWarId = -1;
      this.war.set(null);
    }

    if (warId >= 0 && currentWarId !== warId) {
      this.currentWarId = warId;
      this.warHubService.subscribeToWar(warId);
    }
  }
}
