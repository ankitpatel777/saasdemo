import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./features/auth/pages/login/login').then((m) => m.Login),
  },
  {
    path: ':tenant/admin',
    title: 'Admin',
    loadComponent: () =>
      import('./features/admin/pages/admin-dashboard/admin-dashboard').then((m) => m.AdminDashboard),
  },
  {
    path: ':tenant/user',
    title: 'Dashboard',
    loadComponent: () =>
      import('./features/user/pages/user-dashboard/user-dashboard').then((m) => m.UserDashboard),
  },
  {
    path: '**',
    title: 'Not found',
    loadComponent: () => import('./shared/components/not-found/not-found').then((m) => m.NotFound),
  },
];
