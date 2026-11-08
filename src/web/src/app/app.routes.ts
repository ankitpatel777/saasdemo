import { Routes } from '@angular/router';
import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';
import { tenantGuard } from './core/guards/tenant.guard';

// Guards are UX: they keep people on pages that make sense for them. The API enforces access.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./features/auth/pages/login/login').then((m) => m.Login),
  },
  {
    path: 'unregistered',
    title: 'Organization not registered',
    canActivate: [authGuard],
    loadComponent: () => import('./features/auth/pages/unregistered/unregistered').then((m) => m.Unregistered),
  },
  {
    path: 'forbidden',
    title: 'Access denied',
    canActivate: [authGuard],
    loadComponent: () => import('./features/auth/pages/forbidden/forbidden').then((m) => m.Forbidden),
  },
  {
    path: ':tenant/admin',
    title: 'Admin',
    canActivate: [authGuard, tenantGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/pages/admin-dashboard/admin-dashboard').then((m) => m.AdminDashboard),
  },
  {
    path: ':tenant/user',
    title: 'Dashboard',
    canActivate: [authGuard, tenantGuard], // Admins may open it too
    loadComponent: () =>
      import('./features/user/pages/user-dashboard/user-dashboard').then((m) => m.UserDashboard),
  },
  {
    path: '**',
    title: 'Not found',
    loadComponent: () => import('./shared/components/not-found/not-found').then((m) => m.NotFound),
  },
];
