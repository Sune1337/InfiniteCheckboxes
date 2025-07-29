import { AfterViewInit, ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal, TemplateRef, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter, Subject, takeUntil } from 'rxjs';
import { CheckboxGrid } from '../checkbox-grid/checkbox-grid';
import { CheckboxesHubService } from '#checkboxesHubService';
import { HeaderService } from '../../utils/header.service';
import { UserBalance } from '../../api/models/user-balance';

@Component({
  selector: 'app-checkboxes',
  templateUrl: './checkboxes.html',
  styleUrl: './checkboxes.scss',
  imports: [
    FormsModule,
    CheckboxGrid
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Checkboxes implements OnInit, AfterViewInit, OnDestroy {

  protected pageWidths = [32, 64, 128];
  protected selectedPageWidth = signal<number>(this.pageWidths[0]);
  protected pageInput = signal<string>('');
  protected numberOfChecked = signal<number | null>(null);
  protected user = signal<UserBalance | null>(null)

  @ViewChild(CheckboxGrid)
  private checkboxGrid!: CheckboxGrid;
  @ViewChild('headerTemplate')
  private headerTemplate!: TemplateRef<unknown>;

  private currentPageParam = '';

  private headerService = inject(HeaderService);
  private checkboxHubService = inject(CheckboxesHubService);
  private activatedRoute = inject(ActivatedRoute);
  private router = inject(Router);
  private title = inject(Title);
  private meta = inject(Meta);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    this.title.setTitle('Browse checkboxes');
    this.meta.updateTag({ name: 'description', content: 'Browse checkboxes from the 256 bit address space.' });
  }

  ngOnInit() {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntil(this.ngUnsubscribe)
      )
      .subscribe(() => {
        const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
        if (id) {
          this.goToId(id);
        }
      });

    const id = this.activatedRoute?.snapshot.firstChild?.params['id'];
    if (id) {
      this.currentPageParam = id;
    } else {
      this.router.navigate(['Checkboxes', 'Welcome!'], { replaceUrl: true });
    }
  }

  ngAfterViewInit() {
    // Set header template.
    this.headerService.setHeader(this.headerTemplate);

    if (this.currentPageParam) {
      this.goToId(this.currentPageParam);
    }

    this.checkboxHubService.globalStatistics
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(stats => {
        this.numberOfChecked.set(stats.NumberOfChecked);
      });

    this.checkboxHubService.user
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(user => {
        this.user.set(user);
      });
  }

  private goToId = (id: string): void => {
    this.currentPageParam = id;
    this.pageInput.set(id);
    this.checkboxGrid.navigateToPage(id);
  }

  ngOnDestroy() {
    this.headerService.setHeader(null);

    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected whenSearchButtonClick = async (): Promise<void> => {
    const pageInputText = this.pageInput();
    if (pageInputText === '') {
      return;
    }

    if (this.currentPageParam == pageInputText) {
      this.checkboxGrid.navigateToPage(pageInputText);
    } else {
      this.router.navigate(['Checkboxes', pageInputText]);
    }
  }

  protected whenPageInputKeyDown = (event: KeyboardEvent): void => {
    if (event.key !== 'Enter') {
      return;
    }

    const pageInputText = this.pageInput();
    if (pageInputText === '') {
      return;
    }

    if (this.currentPageParam == pageInputText) {
      this.checkboxGrid.navigateToPage(pageInputText);
    } else {
      this.router.navigate(['Checkboxes', pageInputText]);
    }
  }

  protected whenDialogClick = (event: MouseEvent, helpDialog: HTMLDialogElement): void => {
    if (event.target === helpDialog) {
      helpDialog.close();
    }
  }
}
