import { AfterViewInit, ChangeDetectionStrategy, Component, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { CheckboxGrid } from "../checkbox-grid/checkbox-grid";
import { ReactiveFormsModule } from "@angular/forms";
import { Subject, takeUntil } from 'rxjs';
import { WarHubService, Wars } from '../../../api/war-hub.service';
import { ActivatedRoute, Router } from '@angular/router';
import { War } from '../../../api/models/war';
import { DatePipe } from '@angular/common';

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

  @ViewChild(CheckboxGrid)
  private checkboxGrid!: CheckboxGrid;

  private currentWarId = -1;

  private warHubService = inject(WarHubService);
  private activatedRoute = inject(ActivatedRoute);
  private router = inject(Router);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    // Register callback to handle updates to checkbox-pages.
    this.warHubService.wars
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(wars => this.warUpdated(wars));
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
}
