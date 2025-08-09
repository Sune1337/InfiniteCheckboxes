import { Routes } from '@angular/router';
import { About } from './about/about';

export const routes: Routes = [
  {
    path: 'Checkboxes', loadComponent: () => import('./checkboxes/checkboxes').then(m => m.Checkboxes), children: [
      { path: ':id', loadComponent: () => import('./checkboxes/checkboxes').then(m => m.Checkboxes) }
    ]
  },
  {
    path: 'War', loadComponent: () => import('./war/war').then(m => m.WarComponent), children: [
      { path: ':id', loadComponent: () => import('./war/war').then(m => m.WarComponent) }
    ]
  },
  {
    path: 'Minesweeper', loadComponent: () => import('./mine-sweeper/mine-sweeper').then(m => m.MinesweeperComponent), children: [
      { path: ':id', loadComponent: () => import('./mine-sweeper/mine-sweeper').then(m => m.MinesweeperComponent) }
    ]
  },
  {
    path: 'LightsOut', loadComponent: () => import('./lights-out/lights-out').then(m => m.LightsOutComponent), children: [
      { path: ':id', loadComponent: () => import('./lights-out/lights-out').then(m => m.LightsOutComponent) }
    ]
  },
  { path: 'About', component: About },
  { path: '', component: About, pathMatch: 'full' },
  { path: '**', redirectTo: '/' }
];
