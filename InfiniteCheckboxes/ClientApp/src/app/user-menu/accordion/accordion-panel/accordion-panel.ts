import { AfterViewInit, ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Accordion } from '../accordion';
import { ScrollHeightObserverDirective } from '../../../../utils/scroll-height-observer.directive';

@Component({
  selector: 'app-accordion-panel',
  imports: [
    ScrollHeightObserverDirective
  ],
  templateUrl: './accordion-panel.html',
  styleUrl: './accordion-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccordionPanel implements AfterViewInit {

  public title = input('');

  protected isOpen = signal(false);
  protected scrollHeight = signal(0);

  private accordion = inject(Accordion);

  ngAfterViewInit(): void {
    if (this.isOpen()) {
      this.togglePanel()
    }
  }

  public setOpen = (open: boolean): void => {
    this.isOpen.set(open);
  }

  public togglePanel = (): void => {
    this.accordion.togglePanel(this, this.isOpen());
  }

  protected whenScrollHeightChange = (scrollHeight: number): void => {
    this.scrollHeight.set(scrollHeight);
  }
}
