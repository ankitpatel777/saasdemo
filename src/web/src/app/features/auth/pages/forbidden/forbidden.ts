import { Component, inject } from '@angular/core';
import { SessionService } from '../../../../core/services/session.service';

/** The API answered 403; shows its ProblemDetails detail (e.g. "No role assigned in your organization."). */
@Component({
  selector: 'app-forbidden',
  template: `
    <h1>Access denied</h1>
    <p role="alert" data-testid="forbidden-detail">
      {{ session.errorMessage() ?? 'You do not have access to this page.' }}
    </p>
    <p>If you think this is a mistake, ask your organization's administrator.</p>
    <button type="button" (click)="session.logout()">Sign out</button>
  `,
})
export class Forbidden {
  protected readonly session = inject(SessionService);
}
