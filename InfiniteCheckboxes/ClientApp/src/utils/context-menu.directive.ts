import { Directive, EventEmitter, HostListener, Output } from '@angular/core';

@Directive({
  selector: '[appContextMenu]'
})
export class ContextMenuDirective {
  @Output() appContextMenu = new EventEmitter<UIEvent>();

  private touchTimeout: any;
  private readonly LONG_PRESS_DURATION = 300;
  private isTouch = false; // Flag to track touch interaction
  private isEmitted = false;

  @HostListener('contextmenu', ['$event'])
  onContextMenu(event: MouseEvent) {
    event.preventDefault();
    // Only emit if it's not from a touch event
    if (!this.isTouch) {
      this.appContextMenu.emit(event);
    }

    // Reset the touch flag
    this.isTouch = false;
    this.isEmitted = false;
  }

  @HostListener('touchstart', ['$event'])
  onTouchStart(event: TouchEvent) {
    this.isTouch = true;
    this.isEmitted = false;

    this.touchTimeout = setTimeout(() => {
      this.appContextMenu.emit(event);
      this.isEmitted = true;
    }, this.LONG_PRESS_DURATION);
  }

  @HostListener('touchend', ['$event'])
  onTouchEnd(event: TouchEvent) {
    if (this.isEmitted) {
      event?.preventDefault();
    }
    clearTimeout(this.touchTimeout);

    setTimeout(() => {
      this.isTouch = false;
      this.isEmitted = false;
    }, 100);
  }

  @HostListener('touchmove')
  onTouchMove() {
    clearTimeout(this.touchTimeout);
  }
}
