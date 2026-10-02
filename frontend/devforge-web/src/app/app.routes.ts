import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    title: 'Dashboard · DevForge',
    loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'applications',
    loadChildren: () => import('./features/applications/applications.routes').then((m) => m.APPLICATION_ROUTES),
  },
  {
    path: 'deployments',
    loadChildren: () => import('./features/deployments/deployments.routes').then((m) => m.DEPLOYMENT_ROUTES),
  },
  { path: '**', redirectTo: 'dashboard' },
];
