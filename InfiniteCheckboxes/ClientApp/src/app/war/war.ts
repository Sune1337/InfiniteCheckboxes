import { AfterViewInit, ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal, TemplateRef, ViewChild } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { ReactiveFormsModule } from "@angular/forms";
import { filter, Subject, takeUntil } from 'rxjs';
import { WarHubService, Wars } from '../../../api/war-hub.service';
import { War } from '../../../api/models/war';
import { CheckboxesHubService, CheckboxPageStatistics } from '../../../api/checkboxes-hub.service';
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

  async ngOnInit(): Promise<void> {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntil(this.ngUnsubscribe)
      )
      .subscribe(() => {
        const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
        if (id) {
          this.goToId(parseInt(id));
        }
      });

    const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
    if (id) {
      this.currentWarId = parseInt(id);
    } else {
      // Load current war.
      const currentWarId = await this.warHubService.getCurrentWar();
      this.router.navigate(['War', currentWarId], { replaceUrl: true });
    }
  }

  async ngAfterViewInit(): Promise<void> {
    // Set header template.
    this.headerService.setHeader(this.headerTemplate);

    if (this.currentWarId >= 0) {
      this.goToId(this.currentWarId);
    }
  }

  private goToId = (id: number): void => {
    this.currentWarId = id;
    if (this.currentWarId >= 0) {
      this.warHubService.subscribeToWar(id);
    }
  }

  ngOnDestroy() {
    this.headerService.setHeader(null);

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

  protected gotoCurrentWar = async (): Promise<void> => {
    // Load current war.
    const currentWarId = await this.warHubService.getCurrentWar();
    this.router.navigate(['War', currentWarId], { replaceUrl: true });
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
    if (!stats) {
      return;
    }

    this.numberOfPlayers.set(stats.numberOfSubscribers);
  }
}
