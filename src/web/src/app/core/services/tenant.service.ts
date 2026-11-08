import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { Observable, filter, map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TenantInfo {
  tenantId: string;
  slug: string;
  name: string;
}

/** Tracks the current tenant from the ':tenant' route param and builds tenant-scoped API URLs. */
@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  readonly currentTenant: Signal<string | null> = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.readTenant()),
    ),
    { initialValue: this.readTenant() },
  );

  /** `${apiBaseUrl}/api/${tenant}/${path}` for the current tenant. */
  apiUrl(path: string): string {
    const tenant = this.currentTenant();
    if (!tenant) {
      throw new Error('No tenant in the current route.');
    }
    return `${environment.apiBaseUrl}/api/${encodeURIComponent(tenant)}/${path.replace(/^\/+/, '')}`;
  }

  getInfo(): Observable<TenantInfo> {
    return this.http.get<TenantInfo>(this.apiUrl('info'));
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
