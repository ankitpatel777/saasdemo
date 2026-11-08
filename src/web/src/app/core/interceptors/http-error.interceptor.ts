import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SessionService, problemDetail } from '../services/session.service';

/**
 * Maps API auth errors to pages: 401 -> /login (sign in again), 403 -> /forbidden (with the API's
 * ProblemDetails detail). The error is re-thrown so callers can still react.
 * 401 navigates to the login PAGE rather than starting a login redirect, which avoids a redirect
 * loop if the API keeps rejecting the token (e.g. a misconfigured audience).
 */
export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const session = inject(SessionService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && req.url.startsWith(`${environment.apiBaseUrl}/api/`)) {
        if (error.status === 401) {
          session.markUnauthorized();
          void router.navigateByUrl('/login');
        } else if (error.status === 403) {
          session.reportForbidden(problemDetail(error));
          void router.navigateByUrl('/forbidden');
        }
      }
      return throwError(() => error);
    }),
  );
};
