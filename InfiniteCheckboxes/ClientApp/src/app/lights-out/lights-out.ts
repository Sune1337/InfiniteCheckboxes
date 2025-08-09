import { AfterViewInit, ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal, TemplateRef, ViewChild } from '@angular/core';
import { HeaderService } from '../../utils/header.service';
import { Meta, Title } from '@angular/platform-browser';
import { filter, Subject, takeUntil } from 'rxjs';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { LightsOutHubService } from '#lightsOutHubService';
import { MessageService } from 'primeng/api';
import { LightsOut } from '../../api/models/lights-out';
import { bigIntToHexString } from '../../utils/bigint-utils';
import { Timer } from '../mine-sweeper/timer/timer';
import { Dialog } from 'primeng/dialog';
import { Button } from 'primeng/button';
import { CheckboxGrid } from '../checkbox-grid/checkbox-grid';
import { Select } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { getErrorMessage } from '../../utils/get-error-message';

@Component({
  selector: 'app-lights-out',
  imports: [
    Timer,
    Dialog,
    Button,
    CheckboxGrid,
    Select,
    FormsModule
  ],
  templateUrl: './lights-out.html',
  styleUrl: './lights-out.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LightsOutComponent implements OnInit, AfterViewInit, OnDestroy {

  @ViewChild('headerTemplate')
  private headerTemplate!: TemplateRef<unknown>;

  protected widthOptions = [3, 5, 7, 9];
  protected selectedWidth = signal<any>(this.widthOptions[0]);
  protected currentLightsOutId = signal(BigInt(0));
  protected lightsOut = signal<LightsOut | null>(null);
  protected helpDialogOpen = signal(false);

  private bigintZero = BigInt(0);

  private headerService = inject(HeaderService);
  private title = inject(Title);
  private meta = inject(Meta);
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private lightsOutHubService = inject(LightsOutHubService);
  private messageService = inject(MessageService);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    this.title.setTitle('Lights out');
    this.meta.updateTag({ name: 'description', content: 'Make all checkboxes unchecked.' });

    // Register callback to handle updates to lights-out.
    this.lightsOutHubService.lightOuts
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(lightsOuts => {
        const hexId = bigIntToHexString(this.currentLightsOutId());
        if (lightsOuts[hexId]) {
          const lightsOut = lightsOuts[hexId];
          this.lightsOut.set(lightsOut);
          this.updateSubscriptions(this.currentLightsOutId());
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
        this.updateSubscriptions(id ? BigInt(`0x${id}`) : this.bigintZero);
      });

    const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
    if (id) {
      this.updateSubscriptions(id ? BigInt(`0x${id}`) : this.bigintZero);
    }
  }

  async ngAfterViewInit(): Promise<void> {
    // Set header template.
    this.headerService.setHeader(this.headerTemplate);
  }

  ngOnDestroy() {
    this.updateSubscriptions(this.bigintZero);
    this.headerService.setHeader(null);
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected whenStartClick = async (): Promise<void> => {
    try {
      const lightsOutId = await this.lightsOutHubService.createGame(parseInt(this.selectedWidth()));
      this.router.navigate(['LightsOut', lightsOutId]);
    } catch (error: any) {
      this.messageService.add({ severity: 'error', detail: getErrorMessage(error) });
    }
  }

  private updateSubscriptions = (id: bigint): void => {
    // Update minesweeper subscription.
    let currentLightsOutId = this.currentLightsOutId();
    if (currentLightsOutId && currentLightsOutId !== id) {
      this.lightsOutHubService.unsubscribeToLightsOut(currentLightsOutId);
      currentLightsOutId = this.bigintZero;
      this.lightsOut.set(null);
    }

    if (id && currentLightsOutId !== id) {
      currentLightsOutId = id;
      this.lightsOutHubService.subscribeToLightsOut(id);
    }

    this.currentLightsOutId.set(currentLightsOutId);
  }

}
