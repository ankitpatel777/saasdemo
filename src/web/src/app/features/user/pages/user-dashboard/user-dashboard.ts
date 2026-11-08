import { Component, input } from '@angular/core';
import { TenantInfoCard } from '../../../../shared/components/tenant-info/tenant-info';

@Component({
  selector: 'app-user-dashboard',
  styleUrl: './user-dashboard.scss',
  imports: [TenantInfoCard],
  templateUrl: './user-dashboard.html',
})
export class UserDashboard {
  // Bound from the :tenant route param via withComponentInputBinding().
  readonly tenant = input<string>();
}
