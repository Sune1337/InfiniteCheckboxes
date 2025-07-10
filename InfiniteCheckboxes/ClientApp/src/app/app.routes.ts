import { Routes } from '@angular/router';
import { Checkboxes } from './checkboxes/checkboxes';

export const routes: Routes = [
  { path: '', component: Checkboxes, pathMatch: 'full' },
  { path: ':id', component: Checkboxes, pathMatch: 'full' },
  { path: '**', redirectTo: '', pathMatch: 'full' }
];
