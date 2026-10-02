import { Routes } from '@angular/router';

export const DEPLOYMENT_ROUTES: Routes = [
  {
    path: ':id',
    title: 'Deployment · DevForge',
    loadComponent: () => import('./deployment-detail').then((m) => m.DeploymentDetail),
  },
];
