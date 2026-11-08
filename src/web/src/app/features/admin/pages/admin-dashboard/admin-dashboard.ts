import { Component, input } from '@angular/core';
import { TenantInfoCard } from '../../../../shared/components/tenant-info/tenant-info';

@Component({
  selector: 'app-admin-dashboard',
  styleUrl: './admin-dashboard.scss',
  imports: [TenantInfoCard],
  templateUrl: './admin-dashboard.html',
})
export class AdminDashboard {
  // Bound from the :tenant route param via withComponentInputBinding().
  readonly tenant = input<string>();
}
