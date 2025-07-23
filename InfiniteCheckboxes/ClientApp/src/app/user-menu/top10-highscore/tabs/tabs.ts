import { AfterContentInit, ChangeDetectionStrategy, Component, ContentChildren, QueryList } from '@angular/core';
import { Tab } from './tab/tab';

@Component({
  selector: 'app-tabs',
  imports: [],
  templateUrl: './tabs.html',
  styleUrl: './tabs.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Tabs implements AfterContentInit {

  @ContentChildren(Tab)
  protected tabs!: QueryList<Tab>;

  ngAfterContentInit(): void {
    if (this.tabs.length > 0) {
      this.openTab(this.tabs.first);
    }
  }

  protected openTab = (selectedTab: Tab): void => {
    this.tabs.forEach(tab => tab.active.set(tab == selectedTab));
  }

}
