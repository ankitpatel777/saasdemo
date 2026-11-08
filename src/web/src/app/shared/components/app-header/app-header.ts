import { Component, inject } from '@angular/core';
import { SessionService } from '../../../core/services/session.service';

/** Who is signed in, for which client, with which role. Hidden until the session is ready. */
@Component({
  selector: 'app-header',
  template: `
    @if (session.me(); as me) {
      <header class="toolbar">
        <strong data-testid="tenant-name">{{ me.tenant.name }}</strong>
        <span class="spacer"></span>
        <span data-testid="display-name">{{ me.displayName }}</span>
        <span class="badge" data-testid="role-badge">{{ me.role }}</span>
        <button type="button" (click)="session.logout()">Sign out</button>
      </header>
    }
  `,
  styles: `
    .toolbar { display: flex; align-items: center; gap: 0.75rem; padding: 0.5rem 1rem; border-bottom: 1px solid #ddd; }
    .spacer { flex: 1; }
    .badge { padding: 0.1rem 0.5rem; border-radius: 999px; background: #eef; font-size: 0.8rem; }
  `,
})
export class AppHeader {
  protected readonly session = inject(SessionService);
}
