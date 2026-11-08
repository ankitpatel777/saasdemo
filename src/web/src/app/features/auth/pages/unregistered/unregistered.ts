import { Component, inject } from '@angular/core';
import { SessionService } from '../../../../core/services/session.service';

/** Signed in, but the user's Entra organization has no tenant here yet. */
@Component({
  selector: 'app-unregistered',
  template: `
    <h1>Your organization isn't registered yet</h1>
    <p>
      You signed in as {{ session.account()?.username }}, but your organization doesn't have access to
      this service yet.
    </p>
    <!-- Self-service onboarding arrives in step 7.6. -->
    <p>Organization onboarding is coming soon. In the meantime, please contact your administrator.</p>
    <button type="button" (click)="session.logout()">Sign out</button>
  `,
})
export class Unregistered {
  protected readonly session = inject(SessionService);
}
