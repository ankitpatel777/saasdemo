import { Injectable, Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Tracks the current tenant from the ':tenant' route param and builds tenant-scoped API URLs. */
@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly router = inject(Router);

  readonly currentTenant: Signal<string | null> = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.readTenant()),
    ),
    { initialValue: this.readTenant() },
  );

  /**
   * `${apiBaseUrl}/api/${tenant}/${path}` for the current tenant. Reads the router state directly
   * rather than the signal: pages call this while being activated, before NavigationEnd updates it.
   */
  apiUrl(path: string): string {
    const tenant = this.readTenant();
    if (!tenant) {
      throw new Error('No tenant in the current route.');
    }
    return `${environment.apiBaseUrl}/api/${encodeURIComponent(tenant)}/${path.replace(/^\/+/, '')}`;
  }

  private readTenant(): string | null {
    let route: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    while (route) {
      const tenant = route.paramMap.get('tenant');
      if (tenant) {
        return tenant;
      }
      route = route.firstChild;
    }
    return null;
  }
}
