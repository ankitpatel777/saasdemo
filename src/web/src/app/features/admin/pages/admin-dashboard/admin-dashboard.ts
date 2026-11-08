import { HttpClient } from '@angular/common/http';
import { Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { environment } from '../../../../../environments/environment';
import { SessionService } from '../../../../core/services/session.service';
import { TenantInfoCard } from '../../../../shared/components/tenant-info/tenant-info';

export interface UserSummary {
  id: string;
  email: string;
  displayName: string;
  role: string;
}

/** Placeholder admin dashboard (full UI in step 7.7): who I am + my tenant's users. */
@Component({
  selector: 'app-admin-dashboard',
  styleUrl: './admin-dashboard.scss',
  imports: [TenantInfoCard],
  templateUrl: './admin-dashboard.html',
})
export class AdminDashboard {
  private readonly http = inject(HttpClient);
  protected readonly session = inject(SessionService);

  // Bound from the :tenant route param via withComponentInputBinding(); tenantGuard made it my tenant.
  readonly tenant = input<string>();

  protected readonly users = rxResource({
    params: () => this.tenant(),
    stream: ({ params: tenant }) =>
      this.http.get<UserSummary[]>(`${environment.apiBaseUrl}/api/${encodeURIComponent(tenant)}/users`),
  });
}
