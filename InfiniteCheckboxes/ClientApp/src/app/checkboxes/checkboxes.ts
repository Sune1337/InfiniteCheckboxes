import { AfterViewInit, ChangeDetectionStrategy, Component, inject, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CheckboxGrid } from '../checkbox-grid/checkbox-grid';

@Component({
  selector: 'app-checkboxes',
  templateUrl: './checkboxes.html',
  styleUrl: './checkboxes.scss',
  imports: [
    FormsModule,
    CheckboxGrid,
    RouterLink
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Checkboxes implements AfterViewInit {

  protected pageWidths = [32, 64, 128];
  protected selectedPageWidth = signal<number>(this.pageWidths[0]);
  protected pageInput = signal<string>('');

  @ViewChild(CheckboxGrid)
  private checkboxGrid!: CheckboxGrid;

  private currentPageParam = '';

  private activatedRoute = inject(ActivatedRoute);
  private router = inject(Router);
  private title = inject(Title);
  private meta = inject(Meta);

  constructor(){
    this.title.setTitle('Browse checkboxes');
    this.meta.updateTag({ name: 'description', content: 'Browse checkboxes from the 256 bit address space.' });
  }

  ngAfterViewInit() {
    this.activatedRoute.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.pageInput.set(id);
        this.currentPageParam = id;
        this.checkboxGrid.navigateToPage(id);
      } else {
        this.router.navigate(['Checkboxes', 'Welcome!'], { replaceUrl: true });
      }
    });
  }

  protected whenSearchButtonClick = async (): Promise<void> => {
    const pageInputText = this.pageInput();
    if (pageInputText === '') {
      return;
    }

    if (this.currentPageParam == pageInputText) {
      this.checkboxGrid.navigateToPage(pageInputText);
    } else {
      this.router.navigate(['Checkboxes', pageInputText]);
    }
  }

  protected whenPageInputKeyDown = (event: KeyboardEvent): void => {
    if (event.key !== 'Enter') {
      return;
    }

    const pageInputText = this.pageInput();
    if (pageInputText === '') {
      return;
    }

    if (this.currentPageParam == pageInputText) {
      this.checkboxGrid.navigateToPage(pageInputText);
    } else {
      this.router.navigate(['Checkboxes', pageInputText]);
    }
  }

  protected whenDialogClick = (event: MouseEvent, helpDialog: HTMLDialogElement): void => {
    if (event.target === helpDialog) {
      helpDialog.close();
    }
  }
}
