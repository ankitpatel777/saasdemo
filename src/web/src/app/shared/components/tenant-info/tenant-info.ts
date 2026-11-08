import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TenantService } from '../../../core/services/tenant.service';

/** Shows the current tenant's details from GET /api/{tenant}/info. */
@Component({
  selector: 'app-tenant-info',
  templateUrl: './tenant-info.html',
})
export class TenantInfoCard {
  private readonly tenants = inject(TenantService);

  protected readonly info = rxResource({
    params: () => this.tenants.currentTenant() ?? undefined,
    stream: () => this.tenants.getInfo(),
  });

  protected readonly notFound = computed(() => {
    const error = this.info.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });
}
