import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/**
 * Admins only; Users go to their own dashboard. Awaits bootstrap itself because Angular may start
 * the guards of a route concurrently.
 */
export const adminGuard: CanActivateFn = async () => {
  const session = inject(SessionService);
  const router = inject(Router);

  if ((await session.bootstrap()) !== 'ready') {
    return router.parseUrl(session.homeUrl());
  }

  return session.isAdmin() ? true : router.createUrlTree(['/', session.me()!.tenant.slug, 'user']);
};
