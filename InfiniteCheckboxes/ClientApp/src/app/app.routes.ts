import { Routes } from '@angular/router';
import { Checkboxes } from './checkboxes/checkboxes';
import { WarComponent } from './war/war';

export const routes: Routes = [
  { path: 'Checkboxes', component: Checkboxes, pathMatch: 'full' },
  { path: 'Checkboxes/:id', component: Checkboxes, pathMatch: 'full' },
  { path: 'War/:id', component: WarComponent, pathMatch: 'full' },
  { path: 'War', component: WarComponent, pathMatch: 'full' },
  { path: '**', redirectTo: 'Checkboxes', pathMatch: 'full' }
];
