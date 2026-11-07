import { Component, input } from '@angular/core';

@Component({
  selector: 'app-user-dashboard',
  styleUrl: './user-dashboard.scss',
  templateUrl: './user-dashboard.html',
})
export class UserDashboard {
  // Bound from the :tenant route param via withComponentInputBinding().
  readonly tenant = input<string>();
}
