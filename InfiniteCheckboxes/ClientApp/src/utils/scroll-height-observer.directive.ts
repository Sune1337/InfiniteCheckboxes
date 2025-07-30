import { Directive, ElementRef, EventEmitter, NgZone, OnDestroy, OnInit, Output } from '@angular/core';

@Directive({
  selector: '[scrollHeightObserver]',
  standalone: true
})
export class ScrollHeightObserverDirective implements OnInit, OnDestroy {
  @Output() scrollHeightChange = new EventEmitter<number>();

  private observer!: MutationObserver;
  private resizeObserver!: ResizeObserver;
  private lastScrollHeight: number;

  constructor(private elementRef: ElementRef, private ngZone: NgZone) {
    this.lastScrollHeight = 0;

    // Create observers outside Angular zone
    this.ngZone.runOutsideAngular(() => {
      this.observer = new MutationObserver(() => this.checkScrollHeight());
      this.resizeObserver = new ResizeObserver(() => this.checkScrollHeight());
    });
  }

  private checkScrollHeight(): void {
    const newScrollHeight = this.elementRef.nativeElement.scrollHeight;
    if (newScrollHeight !== this.lastScrollHeight) {
      // Only run inside Angular zone when we need to emit the event
      this.ngZone.run(() => {
        this.lastScrollHeight = newScrollHeight;
        this.scrollHeightChange.emit(newScrollHeight);
      });
    }
  }

  ngOnInit() {
    // Initialize observers outside Angular zone
    this.ngZone.runOutsideAngular(() => {
      this.lastScrollHeight = this.elementRef.nativeElement.scrollHeight;
      this.ngZone.run(() => {
        this.scrollHeightChange.emit(this.lastScrollHeight);
      });

      this.observer.observe(this.elementRef.nativeElement, {
        attributes: true,
        childList: true,
        subtree: true,
        characterData: true
      });

      this.resizeObserver.observe(this.elementRef.nativeElement);
    });
  }

  ngOnDestroy() {
    this.observer.disconnect();
    this.resizeObserver.disconnect();
  }
}
