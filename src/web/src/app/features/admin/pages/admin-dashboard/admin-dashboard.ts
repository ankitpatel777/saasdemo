import { Component, input } from '@angular/core';

@Component({
  selector: 'app-admin-dashboard',
  styleUrl: './admin-dashboard.scss',
  templateUrl: './admin-dashboard.html',
})
export class AdminDashboard {
  // Bound from the :tenant route param via withComponentInputBinding().
  readonly tenant = input<string>();
}
