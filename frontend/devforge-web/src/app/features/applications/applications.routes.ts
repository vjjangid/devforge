import { Routes } from '@angular/router';

export const APPLICATION_ROUTES: Routes = [
  {
    path: '',
    title: 'Applications · DevForge',
    loadComponent: () => import('./application-list').then((m) => m.ApplicationList),
  },
  {
    path: 'new',
    title: 'New application · DevForge',
    loadComponent: () => import('./application-form').then((m) => m.ApplicationForm),
  },
  {
    path: ':id',
    title: 'Application · DevForge',
    loadComponent: () => import('./application-detail').then((m) => m.ApplicationDetail),
  },
  {
    path: ':id/edit',
    title: 'Edit application · DevForge',
    loadComponent: () => import('./application-form').then((m) => m.ApplicationForm),
  },
];
