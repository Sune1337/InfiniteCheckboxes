import { AfterViewInit, ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal, ViewChild, WritableSignal } from '@angular/core';
import { CdkFixedSizeVirtualScroll, CdkVirtualForOf, CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { CheckboxesHubService, CheckboxPages } from '../../../api/checkboxes-hub.service';
import { getCheckboxPageId } from '../../../utils/checkbox-page-id';
import { sortHex } from '../../../utils/hex-string-sorter';
import { FormsModule } from '@angular/forms';

interface IndexItem {
  index: bigint;
  displayValue: string;
  state: WritableSignal<boolean[]>;
}

@Component({
  selector: 'app-checkboxes',
  templateUrl: './checkboxes.html',
  styleUrl: './checkboxes.scss',
  imports: [
    CdkVirtualScrollViewport,
    CdkVirtualForOf,
    CdkFixedSizeVirtualScroll,
    FormsModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Checkboxes implements OnInit, AfterViewInit {

  protected pageWidths = [32, 64, 128];
  protected selectedPageWidth = signal<number>(this.pageWidths[0]);
  protected checkBoxPages = signal<IndexItem[]>([]);
  protected pageInput = signal<string>('');

  // Generate grid-template-columns value.
  protected gridColumns = computed(() => `repeat(${this.selectedPageWidth()}, 24px)`);

  // The returned value of itemSize must match the values in checkboxes.scss.
  protected itemSize = computed<number>(() => 4096 / this.selectedPageWidth() * this.rowHeight);

  @ViewChild(CdkVirtualScrollViewport)
  private viewport!: CdkVirtualScrollViewport;

  @ViewChild(CdkVirtualForOf)
  private virtualFor!: CdkVirtualForOf<any>;

  private readonly defaultPageId: bigint = BigInt(100);
  private readonly MinPageId = BigInt(0);
  private readonly MaxPageId = BigInt('0x' + 'F'.repeat(64)); // 2^256 - 1

  private rowHeight = 24;
  private subscribedPageIds: bigint[] = [];
  private lastWidth = this.selectedPageWidth();

  private checkboxHubService = inject(CheckboxesHubService);

  constructor() {
    // Register callback to handle updates to checkbox-pages.
    this.checkboxHubService.checkboxPages.subscribe(pages => this.checkboxPageUpdated(pages));

    // Handle changes to page-width.
    effect(() => this.whenPageWidthChange(this.selectedPageWidth()));
  }

  ngOnInit() {
    this.checkBoxPages.set([this.createCheckboxPage(this.defaultPageId)]);
  }

  ngAfterViewInit() {
    // Subscribe to view changes
    this.virtualFor.viewChange.subscribe(event => {
      const data = this.checkBoxPages();
      const firstRenderedId = data[event.start].index;
      const lastIndex = event.end >= data.length - 1 ? data.length - 1 : event.end;
      const lastRenderedId = data[lastIndex].index;

      // Stop subscribing to items not rendered.
      for (let i = this.subscribedPageIds.length - 1; i >= 0; i--) {
        const id = this.subscribedPageIds[i];
        if (id < firstRenderedId || id > lastRenderedId) {
          this.checkboxHubService.unsubscribeToCheckboxPage(getCheckboxPageId(id));
          this.subscribedPageIds.splice(i, 1);
        }
      }

      // Start subscribing to new items.
      for (let i = event.start; i <= lastIndex; i++) {
        const id = data[i].index;
        if (this.subscribedPageIds.includes(id)) {
          continue;
        }

        this.checkboxHubService.subscribeToCheckboxPage(getCheckboxPageId(id));
        this.subscribedPageIds.push(id);
      }
    });
  }

  private createCheckboxPage(index: bigint): IndexItem {
    return {
      index,
      displayValue: '0x' + index.toString(16).padStart(64, '0'),
      state: signal(Array(4096))
    };
  }

  private whenPageWidthChange = (width: number) => {
    if (!this.viewport) {
      return;
    }

    const oldItemSize = 4096 / this.lastWidth * this.rowHeight;
    const oldScrollOffset = this.viewport.measureScrollOffset();
    const noItems = oldScrollOffset / oldItemSize;
    let newScrollOffset = noItems * this.itemSize();
    this.lastWidth = this.selectedPageWidth();

    this.viewport.scrollTo({ top: newScrollOffset });
    setTimeout(() => {
      this.viewport.scrollTo({ top: newScrollOffset });

      // Tell viewport to update its size cache
      this.viewport.checkViewportSize();
    });
  }

  onScroll() {
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

  private addItemsAtEnd() {
    const currentItems = this.checkBoxPages();
    if (currentItems.length === 0) return;

    const lastIndex = currentItems[currentItems.length - 1].index;
    if (lastIndex >= this.MaxPageId) return;

    const newItems: IndexItem[] = [];
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

    const firstIndex = currentItems[0].index;
    if (firstIndex <= this.MinPageId) return;

    const newItems: IndexItem[] = [];
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

  private checkboxPageUpdated(checkboxPages: CheckboxPages) {
    const keys = Object.keys(checkboxPages);
    const sortedKeys = sortHex(keys);
    const items = [...this.checkBoxPages()];
    for (const key of sortedKeys) {
      const id = BigInt(`0x${key}`);
      for (const item of items) {
        if (item.index === id) {
          item.state.set(checkboxPages[key]);
        }
      }
    }
    this.checkBoxPages.set(items);
  }

  protected whenCheckboxChanged = (id: bigint, index: number, event: Event): void => {
    const checkboxElement = event.target as HTMLInputElement;
    const isChecked = checkboxElement.checked;
    this.checkboxHubService.setChecked(id.toString(16), index, isChecked);
  }

  protected whenSearchButtonClick = async (): Promise<void> => {
    const pageInputText = this.pageInput();
    if (pageInputText === '') {
      return;
    }

    const pageId = await this.parseStringToBigInt(pageInputText);
    const existingPage = this.checkBoxPages().find(p => p.index === pageId);
    this.checkBoxPages.set([existingPage ?? this.createCheckboxPage(pageId)]);

    // Reset the scroll position to top
    this.viewport.scrollToIndex(0);

    // Force layout recalculation
    this.onScroll();
  }

  protected whenPageInputKeyDown = (event: KeyboardEvent): void => {
    if (event.key === 'Enter') {
      this.whenSearchButtonClick();
    }
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
