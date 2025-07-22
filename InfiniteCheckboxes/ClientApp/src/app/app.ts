import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, ViewChild, ViewContainerRef } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { HeaderService } from '../utils/header.service';
import { UserMenu } from './user-menu/user-menu';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, UserMenu],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App implements OnInit, OnDestroy {

  @ViewChild('headerOutlet', { read: ViewContainerRef })
  private headerOutlet!: ViewContainerRef;
  private headerContentSubscription?: Subscription;

  private headerService = inject(HeaderService);

  ngOnInit() {
    this.headerContentSubscription = this.headerService.headerContent.subscribe(template => {
      if (this.headerOutlet) {
        this.headerOutlet.clear();
        if (template) {
          this.headerOutlet.createEmbeddedView(template);
        }
      }
    });
  }

  ngOnDestroy() {
    this.headerContentSubscription?.unsubscribe();
  }

}
