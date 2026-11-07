import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { HealthService } from '../../../../core/services/health';

@Component({
  selector: 'app-login',
  styleUrl: './login.scss',
  templateUrl: './login.html',
})
export class Login {
  protected readonly apiStatus = toSignal(
    inject(HealthService)
      .getHealth()
      .pipe(
        map((h) => h.status),
        catchError(() => of('unreachable')),
      ),
    { initialValue: 'checking…' },
  );
}
