import { AfterViewInit, Component, inject, TemplateRef, ViewChild } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { HeaderService } from '../../utils/header.service';

@Component({
  selector: 'app-about',
  imports: [],
  templateUrl: './about.html',
  styleUrl: './about.scss'
})
export class About implements AfterViewInit {

  @ViewChild('headerTemplate')
  private headerTemplate!: TemplateRef<unknown>;

  private headerService = inject(HeaderService);
  private title = inject(Title);
  private meta = inject(Meta);

  constructor() {
    this.title.setTitle('About infinite checkboxes');
    this.meta.updateTag({ name: 'description', content: 'Play the checkbox war in real-time with warriors from all over the world. The goal is to check or uncheck all the checkboxes.' });
  }

  ngAfterViewInit(): void {
    // Set header template.
    this.headerService.setHeader(this.headerTemplate);
  }

}
