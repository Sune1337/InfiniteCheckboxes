import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CheckboxesHubService, CheckboxPages } from '../../../api/checkboxes-hub.service';
import { getCheckboxPageId } from '../../../utils/checkbox-page-id';
import { sortHex } from '../../../utils/hex-string-sorter';

export interface CheckboxPage {
  id: string;
  state: boolean[];
}

@Component({
  selector: 'app-checkboxes',
  templateUrl: './checkboxes.html',
  styleUrl: './checkboxes.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Checkboxes {

  protected checkboxPages = signal<CheckboxPage[]>([]);

  private checkboxHubService = inject(CheckboxesHubService);

  constructor() {
    this.checkboxHubService.checkboxPages.subscribe(pages => this.checkboxPageUpdated(pages));
    this.checkboxHubService.subscribeToCheckboxPage(getCheckboxPageId(BigInt(256)));
    this.checkboxHubService.subscribeToCheckboxPage(getCheckboxPageId(BigInt(257)));
  }

  private checkboxPageUpdated(checkboxPages: CheckboxPages) {
    const keys = Object.keys(checkboxPages);
    const sortedKeys = sortHex(keys);
    const sortedCheckboxPages = [];
    for (const key of sortedKeys) {
      sortedCheckboxPages.push({ id: key, state: checkboxPages[key] });
    }
    this.checkboxPages.set(sortedCheckboxPages);
  }

  protected whenCheckboxChanged = (id: string, index: number, event: Event): void => {
    const checkboxElement = event.target as HTMLInputElement;
    const isChecked = checkboxElement.checked;
    this.checkboxHubService.setChecked(id, index, isChecked);
  }
}
