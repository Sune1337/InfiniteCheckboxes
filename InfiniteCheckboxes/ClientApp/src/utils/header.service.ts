import { Injectable, TemplateRef } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class HeaderService {

  private readonly headerContentSubject = new BehaviorSubject<TemplateRef<unknown> | null>(null);
  public headerContent = this.headerContentSubject.asObservable();

  public setHeader = (template: TemplateRef<unknown> | null): void => {
    this.headerContentSubject.next(template);
  }
}
