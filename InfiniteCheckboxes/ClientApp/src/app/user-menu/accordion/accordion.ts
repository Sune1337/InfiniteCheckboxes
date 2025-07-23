import { AfterViewInit, ChangeDetectionStrategy, Component, ContentChildren, QueryList } from '@angular/core';
import { ReactiveFormsModule } from "@angular/forms";
import { AccordionPanel } from './accordion-panel/accordion-panel';

@Component({
  selector: 'app-accordion',
  imports: [
    ReactiveFormsModule
  ],
  templateUrl: './accordion.html',
  styleUrl: './accordion.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Accordion implements AfterViewInit {

  @ContentChildren(AccordionPanel)
  private panels!: QueryList<AccordionPanel>;

  ngAfterViewInit(): void {
    if (this.panels.first) {
      this.panels.first.setOpen(true);
    }
  }

  public togglePanel = (panel: AccordionPanel, isExpanded: boolean): void => {
    for (const p of this.panels.toArray()) {
      if (p === panel) {
        p.setOpen(!isExpanded);
      } else {
        p.setOpen(false);
      }
    }
  }
}
