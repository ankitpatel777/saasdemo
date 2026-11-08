import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/**
 * The :tenant in the URL must be the user's own tenant. Any other slug is replaced by theirs on the
 * same page (/other/admin -> /demo/admin), so another tenant's URL is never shown.
 * Not signed up / no role / error -> the page that explains it.
 */
export const tenantGuard: CanActivateFn = async (route) => {
  const session = inject(SessionService);
  const router = inject(Router);

  if ((await session.bootstrap()) !== 'ready') {
    return router.parseUrl(session.homeUrl());
  }

  const slug = session.me()!.tenant.slug;
  if (route.paramMap.get('tenant') === slug) {
    return true;
  }

  const rest = route.url.slice(1).map((segment) => segment.path); // e.g. ['admin']
  return router.createUrlTree(['/', slug, ...rest]);
};
