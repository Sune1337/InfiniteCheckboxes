import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Tab } from './tabs/tab/tab';
import { Tabs } from './tabs/tabs';
import { HighscoreList } from './highscore-list/highscore-list';

@Component({
  selector: 'app-top10-highscore',
  imports: [
    Tab,
    Tabs,
    HighscoreList,
    DatePipe
  ],
  templateUrl: './top10-highscore.html',
  styleUrl: './top10-highscore.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Top10Highscore {

}
