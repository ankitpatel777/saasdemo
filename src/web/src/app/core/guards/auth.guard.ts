import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/** Signed in (an MSAL account exists), else /login. UX only: the API enforces access. */
export const authGuard: CanActivateFn = () =>
  inject(SessionService).currentAccount() ? true : inject(Router).parseUrl('/login');
