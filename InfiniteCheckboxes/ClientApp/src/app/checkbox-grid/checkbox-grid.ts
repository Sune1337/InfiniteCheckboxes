import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnDestroy, OnInit, output, signal, untracked, ViewChild, WritableSignal } from '@angular/core';
import { Scroller } from 'primeng/scroller';
import { MessageService } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { CheckboxesHubService, CheckboxPage, PageGoldSpots } from '#checkboxesHubService';
import { LimitPipe } from '../../utils/limit-pipe';
import { getErrorMessage } from '../../utils/get-error-message';
import { ContextMenuDirective } from '../../utils/context-menu.directive';

interface CheckboxPageForView {
  pageId: bigint;
  state: WritableSignal<boolean[]>;
  goldSpots: WritableSignal<number[]>;
  checkboxStyles: WritableSignal<(string | null)[]>;
}

@Component({
  selector: 'app-checkbox-grid',
  imports: [
    LimitPipe,
    ContextMenuDirective,
    Scroller
  ],
  templateUrl: './checkbox-grid.html',
  styleUrl: './checkbox-grid.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CheckboxGrid implements OnInit, OnDestroy {

  public gridWidth = input(32);
  public maxSize = input(0);
  public subscribeToStatistics = input(false);
  public locationId = input<string | null>(null);
  public contextClick = output<number>();
  public checkboxStyles = input<(string | null)[]>([]);
  public allowCheckUncheck = input<(index: number, isChecked: boolean) => boolean>();

  // Generate grid-template-columns value.
  protected gridColumns = computed(() => `repeat(${this.gridWidth()}, 24px)`);
  protected checkBoxPages = signal<CheckboxPageForView[] | null>(null);

  // The returned value of itemSize must match the values in checkboxes.scss.
  protected itemSize = computed<number>(() => 4096 / this.gridWidth() * this.rowHeight);

  @ViewChild(Scroller)
  private scroller!: Scroller;

  private rowHeight = 24;
  private subscribedPageIds: bigint[] = [];
  private lastWidth = this.gridWidth();

  private readonly MinPageId = BigInt(0);
  private readonly MaxPageId = BigInt('0x' + 'F'.repeat(64));

  private checkboxHubService = inject(CheckboxesHubService);
  private messageService = inject(MessageService);

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
    effect(() => this.whenPageWidthChange(this.gridWidth(), untracked(this.checkBoxPages)));

    // Navigate to page from input.
    effect(() => {
      const id = this.locationId();
      if (id) {
        this.navigateToPage(id);
      }
    });

    let lastCheckboxStyles = this.checkboxStyles();
    // Combine checkboxStyles and checkboxes when styles change.
    effect(() => {
      if (this.checkboxStyles() !== lastCheckboxStyles) {
        this.combineCheckboxStyles(this.checkboxStyles());
        lastCheckboxStyles = this.checkboxStyles();
      }
    });
  }

  ngOnInit() {
    // Emit a default empty checkbox page.
    this.checkBoxPages.set([this.createCheckboxPage(this.MinPageId)]);
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
      this.messageService.add({ severity: 'error', detail: getErrorMessage(error) });
      return;
    }

    const newPages = [];
    let scrollTop = 0;
    let startRenderPageId = 0;
    const pageIdMinusOne = pageId - BigInt(1);
    if (!this.maxSize() && pageIdMinusOne >= 0) {
      newPages.push(this.checkBoxPages()?.find(p => p.pageId === pageIdMinusOne) ?? this.createCheckboxPage(pageIdMinusOne));
      scrollTop = this.itemSize();
      startRenderPageId = 1;
    }

    newPages.push(this.checkBoxPages()?.find(p => p.pageId === pageId) ?? this.createCheckboxPage(pageId));

    const pageIdPlusOne = pageId + BigInt(1);
    if (!this.maxSize() && pageIdPlusOne <= this.MaxPageId) {
      newPages.push(this.checkBoxPages()?.find(p => p.pageId === pageIdPlusOne) ?? this.createCheckboxPage(pageIdPlusOne));
    }
    this.checkBoxPages.set(newPages);
    this.syncSubscriptions(startRenderPageId, startRenderPageId);

    setTimeout(() => this.scroller.scrollTo({ top: scrollTop }));
  }

  protected onScroll = (): void => {
    if (!this.scroller || this.maxSize()) {
      return;
    }

    const scrollTop = this.scroller.getElementRef().nativeElement.scrollTop;
    const clientHeight = this.scroller.getElementRef().nativeElement.clientHeight;
    const firstRenderedIndex = Math.floor(scrollTop / this.itemSize());
    const lastRenderedIndex = Math.floor((scrollTop + clientHeight) / this.itemSize());
    const total = this.checkBoxPages()?.length ?? 0;

    // If we're near the end, add more items
    if (lastRenderedIndex >= total - 1) {
      this.addItemsAtEnd();
    }

    // If we're near the start, add more items
    let addedItems = 0;
    if (firstRenderedIndex < 1) {
      addedItems = this.addItemsAtStart(scrollTop);
    }

    this.syncSubscriptions(firstRenderedIndex + addedItems, lastRenderedIndex + addedItems);
  }

  protected whenCheckboxChanged = async (event: Event): Promise<void> => {
    const checkbox = event.target as HTMLInputElement;
    if (checkbox.tagName !== 'INPUT') {
      return;
    }

    const checkboxElement = event.target as HTMLInputElement;
    const isChecked = checkboxElement.checked;

    const index = parseInt(checkbox.getAttribute('data-index') ?? '', 10);
    if (isNaN(index)) {
      return;
    }

    const allowCheckUncheck = this.allowCheckUncheck();
    if (allowCheckUncheck && !allowCheckUncheck(index, isChecked)) {
      checkboxElement.checked = !isChecked;
      return;
    }

    const pageId = checkboxElement.closest('.scroll-item')?.getAttribute('page-id')
    if (!pageId) {
      return;
    }

    try {
      await this.checkboxHubService.setChecked(BigInt(pageId), index, isChecked);
    } catch (error: any) {
      checkboxElement.checked = !isChecked
      this.messageService.add({ severity: 'error', detail: getErrorMessage(error) });
    }
  }

  protected whenContextMenu = (event: Event): void => {
    const checkbox = event.target as HTMLInputElement;
    if (checkbox.tagName !== 'INPUT') {
      return;
    }

    const index = parseInt(checkbox.getAttribute('data-index') ?? '', 10);
    if (isNaN(index)) {
      return;
    }

    event.preventDefault();
    this.contextClick.emit(index);
  }


  protected trackCheckboxPage = (index: number, item: CheckboxPageForView): any => {
    return item.pageId;
  }

  protected getCheckboxClasses = (index: number, goldSpots: number[], checkboxStyles: (string | null)[]): { [p: string]: boolean } => {
    const result: { [key: string]: boolean } = {};
    if (goldSpots.includes(index)) {
      result['gold-spot'] = true;
    }

    const checkboxStyle = checkboxStyles[index];
    if (checkboxStyle) {
      result[checkboxStyle] = true;
    }

    return result;
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

  private checkboxPageUpdated(updatedCheckboxPage: CheckboxPage) {
    const checkboxPages = [...this.checkBoxPages() || []];

    const id = BigInt(`0x${updatedCheckboxPage.id}`);
    for (const item of checkboxPages) {
      if (item.pageId === id) {
        item.state.set(updatedCheckboxPage.state);
        item.checkboxStyles.set(this.checkboxStyles());
      }
    }

    this.checkBoxPages.set(checkboxPages);
  }

  private goldSpotsUpdated = (goldSpots: PageGoldSpots): void => {
    const checkboxPages = [...this.checkBoxPages() || []];
    const goldSpotsPageId = BigInt(`0x${goldSpots.id}`);
    const checkboxPage = checkboxPages.find(p => p.pageId === goldSpotsPageId);
    if (!checkboxPage) {
      return;
    }

    checkboxPage.goldSpots.set(goldSpots.state);
    this.checkBoxPages.set(checkboxPages);
  }

  private whenPageWidthChange = (gridWidth: number, checkboxPages: CheckboxPageForView[] | null) => {
    const nativeElement = this.scroller?.getElementRef()?.nativeElement;
    if (!nativeElement || !checkboxPages) {
      return;
    }

    const oldItemSize = 4096 / this.lastWidth * this.rowHeight;
    const itemIndexAtTop = nativeElement.scrollTop / oldItemSize;
    const newScrollOffset = itemIndexAtTop * this.itemSize();
    const currentContentSize = checkboxPages.length * oldItemSize;
    this.lastWidth = gridWidth;

    if (newScrollOffset + nativeElement.clientHeight < currentContentSize) {
      nativeElement.scrollTo({ top: newScrollOffset });
    } else {
      // We want to scroll further down than can currently be rendered. Must let the scroller re-render.
      setTimeout(() => {
        nativeElement.scrollTo({ top: newScrollOffset });
      });
    }
  }

  private addItemsAtEnd() {
    const currentItems = this.checkBoxPages();
    if (!currentItems?.length) return;

    const lastIndex = currentItems[currentItems.length - 1].pageId;
    if (lastIndex >= this.MaxPageId) return;

    const newItems: CheckboxPageForView[] = [];
    let nextIndex = lastIndex + BigInt(1);

    for (let i = 0; i < 1 && nextIndex <= this.MaxPageId; i++) {
      newItems.push(this.createCheckboxPage(nextIndex));
      nextIndex = nextIndex + BigInt(1);
    }

    this.checkBoxPages.set([...currentItems, ...newItems]);
  }

  private addItemsAtStart(scrollTop: any): number {
    const currentItems = this.checkBoxPages();
    if (!currentItems?.length) return 0;

    const firstIndex = currentItems[0].pageId;
    if (firstIndex <= this.MinPageId) return 0;

    const newItems: CheckboxPageForView[] = [];
    let prevIndex = firstIndex - BigInt(1);

    for (let i = 0; i < 1 && prevIndex >= this.MinPageId; i++) {
      newItems.unshift(this.createCheckboxPage(prevIndex));
      prevIndex = prevIndex - BigInt(1);
    }

    // Maintain scroll position when adding items at start
    this.checkBoxPages.set([...newItems, ...currentItems]);
    setTimeout(() => {
      const newScrollOffset = scrollTop + (newItems.length * this.itemSize());
      this.scroller.scrollTo({ top: newScrollOffset });
    });

    return newItems.length;
  }

  private combineCheckboxStyles = (checkboxStyles: (string | null)[]): void => {
    const checkboxPages = this.checkBoxPages();
    if (!checkboxPages || !checkboxStyles) {
      return;
    }

    for (const checkboxPage of checkboxPages) {
      checkboxPage.checkboxStyles.set(checkboxStyles);
    }

    this.checkBoxPages.set([...checkboxPages]);
  }

  private createCheckboxPage(pageId: bigint): CheckboxPageForView {
    return {
      pageId: pageId,
      state: signal(Array(4096)),
      goldSpots: signal([]),
      checkboxStyles: signal([])
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
