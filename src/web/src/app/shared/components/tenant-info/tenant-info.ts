import { HttpErrorResponse } from '@angular/common/http';
import { Component, InjectionToken, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { environment } from '../../../../environments/environment';
import { TenantService } from '../../../core/services/tenant.service';

/**
 * Whether the tenant-info card is shown. GET /api/{tenant}/info is anonymous and the API maps it
 * only in Development, so outside development the card renders nothing and makes no request.
 * A protected info endpoint arrives in step 7.4 (issue #5).
 */
export const TENANT_INFO_ENABLED = new InjectionToken<boolean>('TENANT_INFO_ENABLED', {
  providedIn: 'root',
  factory: () => !environment.production,
});

/** Shows the current tenant's details from GET /api/{tenant}/info (development only, see above). */
@Component({
  selector: 'app-tenant-info',
  templateUrl: './tenant-info.html',
})
export class TenantInfoCard {
  private readonly tenants = inject(TenantService);
  protected readonly enabled = inject(TENANT_INFO_ENABLED);

  // Undefined params keep rxResource idle, so when disabled no HTTP call is ever issued.
  protected readonly info = rxResource({
    params: () => (this.enabled ? (this.tenants.currentTenant() ?? undefined) : undefined),
    stream: () => this.tenants.getInfo(),
  });

  protected readonly notFound = computed(() => {
    const error = this.info.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });
}
