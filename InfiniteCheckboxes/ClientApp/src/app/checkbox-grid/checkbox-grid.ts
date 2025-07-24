import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnDestroy, signal, ViewChild, WritableSignal } from '@angular/core';
import { CdkFixedSizeVirtualScroll, CdkVirtualForOf, CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { Subject, takeUntil } from 'rxjs';
import { CheckboxesHubService, CheckboxPages, GoldSpots } from '../../../api/checkboxes-hub.service';
import { LimitPipe } from '../../utils/limit-pipe';
import { getErrorMessage } from '../../utils/get-error-message';

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
    CdkVirtualScrollViewport,
    LimitPipe
  ],
  templateUrl: './checkbox-grid.html',
  styleUrl: './checkbox-grid.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CheckboxGrid implements OnDestroy {

  public gridWidth = input(32);
  public maxSize = input(0);
  public subscribeToStatistics = input(false);

  // Generate grid-template-columns value.
  protected gridColumns = computed(() => `repeat(${this.gridWidth()}, 24px)`);
  protected checkBoxPages = signal<CheckboxPage[] | null>(null);

  // The returned value of itemSize must match the values in checkboxes.scss.
  protected itemSize = computed<number>(() => 4096 / this.gridWidth() * this.rowHeight);

  @ViewChild(CdkVirtualScrollViewport)
  private viewport!: CdkVirtualScrollViewport;

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
    effect(() => this.whenPageWidthChange(this.gridWidth()));
  }

  ngOnDestroy() {
    for (const id of this.subscribedPageIds) {
      this.checkboxHubService.unsubscribeToCheckboxPage(id);
    }

    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  public navigateToPage = async (id: string): Promise<void> => {
    let pageId: bigint;
    try {
      pageId = await this.parseStringToBigInt(id);
    } catch (error: any) {
      alert(getErrorMessage(error));
      return;
    }

    const existingPage = this.checkBoxPages()?.find(p => p.pageId === pageId);
    this.checkBoxPages.set([existingPage ?? this.createCheckboxPage(pageId)]);

    if (this.viewport) {
      // Reset the scroll position to top
      this.viewport.scrollToIndex(0);

      const firstRenderedItem = this.viewport.getRenderedRange().start;
      if (firstRenderedItem == 0) {
        // We're currently watching the first page, so the onscroll event will not be triggered by the virtual scroll.
        this.onScroll();
      }
    }
  }

  protected onScroll = (): void => {
    if (!this.viewport) {
      return;
    }

    if (this.maxSize() > 0) {
      this.syncSubscriptions(0, 0);
      return;
    }

    const renderedRange = this.viewport.getRenderedRange();
    const total = this.viewport.getDataLength();
    if (renderedRange.start == 0 && renderedRange.end == 0) {
      // Viewport has not rendered yet.
      return;
    }

    // If we're near the end, add more items
    if (renderedRange.end > total - 1) {
      this.addItemsAtEnd();
    }

    // If we're near the start, add more items
    let addedItems = 0;
    if (renderedRange.start < 1) {
      addedItems = this.addItemsAtStart();
    }

    this.syncSubscriptions(renderedRange.start + addedItems, renderedRange.end + addedItems);
  }

  protected whenCheckboxChanged = async (id: bigint, index: number, event: Event): Promise<void> => {
    const checkboxElement = event.target as HTMLInputElement;
    const isChecked = checkboxElement.checked;

    try {
      await this.checkboxHubService.setChecked(id, index, isChecked);
    } catch (error: any) {
      checkboxElement.checked = !isChecked
      alert(getErrorMessage(error));
    }
  }

  protected trackCheckboxPage = (index: number, item: CheckboxPage): any => {
    return item.pageId;
  }

  private syncSubscriptions = (start: number, end: number): void => {
    const data = this.checkBoxPages();
    if (!data?.length) return;

    const visibleCheckboxPagesRange = {
      first: data[start].pageId,
      last: data[Math.min(end, data.length - 1)].pageId
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
    for (let i = start; i <= Math.min(end, data.length - 1); i++) {
      const id = data[i].pageId;
      if (this.subscribedPageIds.includes(id)) continue;

      this.checkboxHubService.subscribeToCheckboxPage(id, this.subscribeToStatistics());
      this.subscribedPageIds.push(id);
    }
  }

  private checkboxPageUpdated(updatedCheckboxPages: CheckboxPages) {
    const checkboxPages = [...this.checkBoxPages() || []];

    for (const key of Object.keys(updatedCheckboxPages)) {
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
    const checkboxPages = [...this.checkBoxPages() || []];
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

  private whenPageWidthChange = (gridWidth: number) => {
    if (!this.viewport) {
      return;
    }

    const oldItemSize = 4096 / this.lastWidth * this.rowHeight;
    const itemIndexAtTop = this.viewport.measureScrollOffset() / oldItemSize;
    const newScrollOffset = itemIndexAtTop * this.itemSize();
    const currentContentSize = this.viewport.getDataLength() * oldItemSize;
    this.lastWidth = gridWidth;

    if (newScrollOffset + this.viewport.getViewportSize() < currentContentSize) {
      this.viewport.scrollTo({ top: newScrollOffset });
    } else {
      // We want to scroll further down than can currently be rendered. Must let the scroll-viewport re-render.
      setTimeout(() => {
        this.viewport.scrollTo({ top: newScrollOffset });
      });
    }
  }

  private addItemsAtEnd() {
    const currentItems = this.checkBoxPages();
    if (!currentItems?.length) return;

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

  private addItemsAtStart(): number {
    const currentItems = this.checkBoxPages();
    if (!currentItems?.length) return 0;

    const firstIndex = currentItems[0].pageId;
    if (firstIndex <= this.MinPageId) return 0;

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

    return newItems.length;
  }

  private createCheckboxPage(pageId: bigint): CheckboxPage {
    return {
      pageId: pageId,
      state: signal(Array(4096)),
      goldSpots: signal([])
    };
  }

  private async parseStringToBigInt(input: string): Promise<bigint> {
    let id: bigint;

    if (/^\d+$/.test(input)) {
      // String contains only digits.
      id = BigInt(input);
    } else if (/^0x[0-9a-fA-F]+$/.test(input)) {
      // Check if it's a hex number starting with 0x
      id = BigInt(input);
    } else {
      // Otherwise, calculate SHA256 hash and convert to BigInt
      const encoder = new TextEncoder();
      const data = encoder.encode(input);
      const hashBuffer = await crypto.subtle.digest('SHA-256', data);
      const hashArray = Array.from(new Uint8Array(hashBuffer));
      const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
      id = BigInt('0x' + hashHex);
    }

    if (id < this.MinPageId) {
      throw new Error(`Page id is too small.`);
    } else if (id > this.MaxPageId) {
      throw new Error('Page id is too large.');
    }

    return id;
  }
}
