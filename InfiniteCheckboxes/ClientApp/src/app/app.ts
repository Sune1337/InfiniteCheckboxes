import { Component, inject, OnDestroy, OnInit, ViewChild, ViewContainerRef } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { HeaderService } from '../utils/header.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
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
