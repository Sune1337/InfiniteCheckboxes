import { Directive, ElementRef, EventEmitter, OnDestroy, OnInit, Output } from '@angular/core';

@Directive({
  selector: '[scrollHeightObserver]',
  standalone: true
})
export class ScrollHeightObserverDirective implements OnInit, OnDestroy {
  @Output() scrollHeightChange = new EventEmitter<number>();

  private observer: MutationObserver;
  private lastScrollHeight: number;

  constructor(private elementRef: ElementRef) {
    this.lastScrollHeight = this.elementRef.nativeElement.scrollHeight;

    this.observer = new MutationObserver(() => {
      const newScrollHeight = this.elementRef.nativeElement.scrollHeight;
      if (newScrollHeight !== this.lastScrollHeight) {
        this.lastScrollHeight = newScrollHeight;
        this.scrollHeightChange.emit(newScrollHeight);
      }
    });
  }

  ngOnInit() {
    this.observer.observe(this.elementRef.nativeElement, {
      attributes: true,
      childList: true,
      subtree: true
    });
  }

  ngOnDestroy() {
    this.observer.disconnect();
  }
}
