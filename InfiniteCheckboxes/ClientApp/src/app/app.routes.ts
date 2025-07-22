import { Routes } from '@angular/router';
import { Checkboxes } from './checkboxes/checkboxes';
import { WarComponent } from './war/war';

export const routes: Routes = [
  {
    path: 'Checkboxes', component: Checkboxes, children: [
      { path: ':id', component: Checkboxes }
    ]
  },
  {
    path: 'War', component: WarComponent, children: [
      { path: ':id', component: WarComponent }
    ]
  },
  { path: '**', redirectTo: 'Checkboxes', pathMatch: 'full' }
];
