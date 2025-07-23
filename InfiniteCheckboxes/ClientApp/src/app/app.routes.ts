import { Routes } from '@angular/router';
import { Checkboxes } from './checkboxes/checkboxes';
import { WarComponent } from './war/war';
import { About } from './about/about';

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
  { path: 'About', component: About },
  { path: '', component: About, pathMatch: 'full' },
  { path: '**', redirectTo: '/' }
];
