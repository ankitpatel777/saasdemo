import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { HealthService } from '../../../../core/services/health';
import { SessionService } from '../../../../core/services/session.service';

/**
 * Sign-in page. After the Entra redirect returns (handled at startup), it loads the session and
 * sends the user on: Admin -> /{slug}/admin, User -> /{slug}/user, or the page explaining why not.
 */
@Component({
  selector: 'app-login',
  styleUrl: './login.scss',
  templateUrl: './login.html',
})
export class Login {
  private readonly router = inject(Router);
  protected readonly session = inject(SessionService);
  protected readonly redirecting = signal(false);

  protected readonly apiStatus = toSignal(
    inject(HealthService)
      .getHealth()
      .pipe(
        map((h) => h.status),
        catchError(() => of('unreachable')),
      ),
    { initialValue: 'checking…' },
  );

  constructor() {
    if (this.session.currentAccount()) {
      void this.session.bootstrap().then((status) => {
        if (status === 'ready' || status === 'unregistered' || status === 'forbidden') {
          void this.router.navigateByUrl(this.session.homeUrl());
        }
      });
    }
  }

  protected signIn(): void {
    this.redirecting.set(true);
    void this.session.login();
  }
}
