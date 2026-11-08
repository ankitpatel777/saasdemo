import { Component, inject, input } from '@angular/core';
import { SessionService } from '../../../../core/services/session.service';
import { TenantInfoCard } from '../../../../shared/components/tenant-info/tenant-info';

/** Placeholder user dashboard (full UI in step 7.7). */
@Component({
  selector: 'app-user-dashboard',
  styleUrl: './user-dashboard.scss',
  imports: [TenantInfoCard],
  templateUrl: './user-dashboard.html',
})
export class UserDashboard {
  protected readonly session = inject(SessionService);

  // Bound from the :tenant route param via withComponentInputBinding(); tenantGuard made it my tenant.
  readonly tenant = input<string>();
}
