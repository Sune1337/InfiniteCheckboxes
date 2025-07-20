import { AfterViewInit, ChangeDetectionStrategy, Component, computed, effect, inject, input, OnDestroy, signal, ViewChild, WritableSignal } from '@angular/core';
import { CdkFixedSizeVirtualScroll, CdkVirtualForOf, CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { CheckboxesHubService, CheckboxPages, GoldSpots } from '../../../api/checkboxes-hub.service';
import { sortHex } from '../../../utils/hex-string-sorter';
import { Subject, takeUntil } from 'rxjs';

interface CheckboxPage {
  pageId: bigint;
  state: WritableSignal<boolean[]>;
  goldSpots: WritableSignal<number[]>;
}

@Component({
  selector: 'app-checkbox-grid',
  imports: [
    CdkFixedSizeVirtualScroll,
    CdkVirtualForOf,
    CdkVirtualScrollViewport
  ],
  templateUrl: './checkbox-grid.html',
  styleUrl: './checkbox-grid.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CheckboxGrid implements AfterViewInit, OnDestroy {

  public gridWidth = input(32);
  public maxSize = input(0);

  // Generate grid-template-columns value.
  protected gridColumns = computed(() => `repeat(${this.gridWidth()}, 24px)`);
  protected checkBoxPages = signal<CheckboxPage[]>([]);

  // The returned value of itemSize must match the values in checkboxes.scss.
  protected itemSize = computed<number>(() => 4096 / this.gridWidth() * this.rowHeight);

  @ViewChild(CdkVirtualScrollViewport)
  private viewport!: CdkVirtualScrollViewport;

  @ViewChild(CdkVirtualForOf)
  private virtualFor!: CdkVirtualForOf<any>;

  private rowHeight = 24;
  private subscribedPageIds: bigint[] = [];
  private lastWidth = this.gridWidth();

  private readonly MinPageId = BigInt(0);
  private readonly MaxPageId = BigInt('0x' + 'F'.repeat(64));

  private checkboxHubService = inject(CheckboxesHubService);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  constructor() {
    // Register callback to handle updates to checkbox-pages.
    this.checkboxHubService.checkboxPages
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(pages => this.checkboxPageUpdated(pages));

    // Register callback to handle updates to gold-spots.
    this.checkboxHubService.goldSpots
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(goldSpots => this.goldSpotsUpdated(goldSpots));

    // Handle changes to page-width.
    effect(() => this.whenPageWidthChange());
  }

  ngAfterViewInit() {
    // Subscribe to view changes
    this.virtualFor.viewChange.subscribe(event => {
      const data = this.checkBoxPages();
      if (data.length === 0) return;

      const visibleCheckboxPagesRange = {
        first: data[event.start].pageId,
        last: data[Math.min(event.end, data.length - 1)].pageId
      };

      // Stop subscribing to items not rendered.
      for (let i = this.subscribedPageIds.length - 1; i >= 0; i--) {
        const id = this.subscribedPageIds[i];
        if (id < visibleCheckboxPagesRange.first || id > visibleCheckboxPagesRange.last) {
          this.checkboxHubService.unsubscribeToCheckboxPage(id);
          this.subscribedPageIds.splice(i, 1);
        }
      }

      // Start subscribing to new items.
      for (let i = event.start; i <= Math.min(event.end, data.length - 1); i++) {
        const id = data[i].pageId;
        if (this.subscribedPageIds.includes(id)) continue;

        this.checkboxHubService.subscribeToCheckboxPage(id);
        this.subscribedPageIds.push(id);
      }
    });
  }

  ngOnDestroy() {
    for (const id of this.subscribedPageIds) {
      this.checkboxHubService.unsubscribeToCheckboxPage(id);
    }

    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  public navigateToPage = (id: string): void => {
    // For some reason initial scrolling on page-reload does not work unless we do the initial navigation in setTimeout here.
    // If we don't. scrolling works when using a page-id that needs to be hashed; and do not work when using a number or hex-string.
    this.checkBoxPages.set([]);

    setTimeout(() => {
      this.goToPage(id);
    });
  }

  protected onScroll = (): void => {
    if (this.maxSize() > 0) {
      return
    }

    const renderedRange = this.viewport.getRenderedRange();
    const total = this.viewport.getDataLength();

    // If we're near the end, add more items
    if (renderedRange.end > total - 1) {
      this.addItemsAtEnd();
    }

    // If we're near the start, add more items
    if (renderedRange.start < 1) {
      this.addItemsAtStart();
    }
  }

  protected whenCheckboxChanged = async (id: bigint, index: number, event: Event): Promise<void> => {
    const checkboxElement = event.target as HTMLInputElement;
    const isChecked = checkboxElement.checked;

    try {
      await this.checkboxHubService.setChecked(id, index, isChecked);
    } catch (error: any) {
      checkboxElement.checked = !isChecked
      alert(error.message);
    }
  }

  protected trackCheckboxPage = (index: number, item: CheckboxPage): any => {
    return item.pageId;
  }

  private checkboxPageUpdated(updatedCheckboxPages: CheckboxPages) {
    const keys = Object.keys(updatedCheckboxPages);
    const sortedKeys = sortHex(keys);
    const checkboxPages = [...this.checkBoxPages()];

    for (const key of sortedKeys) {
      const id = BigInt(`0x${key}`);
      for (const item of checkboxPages) {
        if (item.pageId === id) {
          item.state.set(updatedCheckboxPages[key]);
        }
      }
    }

    this.checkBoxPages.set(checkboxPages);
  }

  private goldSpotsUpdated = (goldSpots: GoldSpots): void => {
    const checkboxPages = [...this.checkBoxPages()];
    const keys = Object.keys(goldSpots);

    for (const key of keys) {
      const pageId = BigInt(`0x${key}`);
      const checkboxPage = checkboxPages.find(p => p.pageId === pageId);
      if (!checkboxPage) {
        continue;
      }

      checkboxPage.goldSpots.set(goldSpots[key]);
    }

    this.checkBoxPages.set(checkboxPages);
  }

  private whenPageWidthChange = () => {
    if (!this.viewport) {
      return;
    }

    const oldItemSize = 4096 / this.lastWidth * this.rowHeight;
    const oldScrollOffset = this.viewport.measureScrollOffset();
    const noItems = oldScrollOffset / oldItemSize;
    let newScrollOffset = noItems * this.itemSize();
    this.lastWidth = this.gridWidth();

    this.viewport.scrollTo({ top: newScrollOffset });
    setTimeout(() => {
      this.viewport.scrollTo({ top: newScrollOffset });

      // Tell viewport to update its size cache
      this.viewport.checkViewportSize();
    });
  }

  private addItemsAtEnd() {
    const currentItems = this.checkBoxPages();
    if (currentItems.length === 0) return;

    const lastIndex = currentItems[currentItems.length - 1].pageId;
    if (lastIndex >= this.MaxPageId) return;

    const newItems: CheckboxPage[] = [];
    let nextIndex = lastIndex + BigInt(1);

    for (let i = 0; i < 5 && nextIndex <= this.MaxPageId; i++) {
      newItems.push(this.createCheckboxPage(nextIndex));
      nextIndex = nextIndex + BigInt(1);
    }

    this.checkBoxPages.set([...currentItems, ...newItems]);
  }

  private addItemsAtStart() {
    const currentItems = this.checkBoxPages();
    if (currentItems.length === 0) return;

    const firstIndex = currentItems[0].pageId;
    if (firstIndex <= this.MinPageId) return;

    const newItems: CheckboxPage[] = [];
    let prevIndex = firstIndex - BigInt(1);

    for (let i = 0; i < 5 && prevIndex >= this.MinPageId; i++) {
      newItems.unshift(this.createCheckboxPage(prevIndex));
      prevIndex = prevIndex - BigInt(1);
    }

    // Maintain scroll position when adding items at start
    const oldScrollOffset = this.viewport.measureScrollOffset();
    this.checkBoxPages.set([...newItems, ...currentItems]);
    setTimeout(() => {
      const newScrollOffset = oldScrollOffset + (newItems.length * this.itemSize());
      this.viewport.scrollTo({ top: newScrollOffset });
    });
  }

  private createCheckboxPage(pageId: bigint): CheckboxPage {
    return {
      pageId: pageId,
      state: signal(Array(4096)),
      goldSpots: signal([])
    };
  }

  private goToPage = async (id: string): Promise<void> => {
    const pageId = await this.parseStringToBigInt(id);
    const existingPage = this.checkBoxPages().find(p => p.pageId === pageId);
    this.checkBoxPages.set([existingPage ?? this.createCheckboxPage(pageId)]);

    // Reset the scroll position to top
    this.viewport.scrollToIndex(0);

    // Force layout recalculation
    this.onScroll();
  }

  private async parseStringToBigInt(input: string): Promise<bigint> {
    // Check if string contains only digits
    if (/^\d+$/.test(input)) {
      return BigInt(input);
    }

    // Check if it's a hex number starting with 0x
    if (/^0x[0-9a-fA-F]+$/.test(input)) {
      return BigInt(input);
    }

    // Otherwise, calculate SHA256 hash and convert to BigInt
    const encoder = new TextEncoder();
    const data = encoder.encode(input);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
    return BigInt('0x' + hashHex);
  }
}
