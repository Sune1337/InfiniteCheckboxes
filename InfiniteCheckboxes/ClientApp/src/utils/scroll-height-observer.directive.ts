import { Directive, ElementRef, EventEmitter, OnDestroy, OnInit, Output } from '@angular/core';

@Directive({
  selector: '[scrollHeightObserver]',
  standalone: true
})
export class ScrollHeightObserverDirective implements OnInit, OnDestroy {
  @Output() scrollHeightChange = new EventEmitter<number>();

  private resizeObserver: ResizeObserver;
  private lastScrollHeight: number;

  constructor(private elementRef: ElementRef) {
    this.lastScrollHeight = this.elementRef.nativeElement.scrollHeight;

    this.resizeObserver = new ResizeObserver(() => {
      const newScrollHeight = this.elementRef.nativeElement.scrollHeight;
      if (newScrollHeight !== this.lastScrollHeight) {
        this.lastScrollHeight = newScrollHeight;
        this.scrollHeightChange.emit(newScrollHeight);
      }
    });
  }

  ngOnInit() {
    this.resizeObserver.observe(this.elementRef.nativeElement);
  }

  ngOnDestroy() {
    this.resizeObserver.disconnect();
  }
}
