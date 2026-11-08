import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { AccountInfo, BrowserAuthError, BrowserAuthErrorCodes } from '@azure/msal-browser';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export type SessionStatus = 'anonymous' | 'loading' | 'ready' | 'unregistered' | 'forbidden' | 'error';

export interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
  tenant: { slug: string; name: string };
}

interface MyTenantResponse {
  slug: string;
  name: string;
}

/** Reads the human-readable reason from an API ProblemDetails error, if there is one. */
export function problemDetail(error: unknown): string | null {
  if (error instanceof HttpErrorResponse && typeof error.error === 'object' && error.error) {
    const detail = (error.error as { detail?: unknown }).detail;
    return typeof detail === 'string' ? detail : null;
  }
  return null;
}

/**
 * Who is signed in and where they belong. bootstrap() asks the API once per app lifetime:
 * my organization's tenant (/api/platform/my-tenant), then me in that tenant (/api/{slug}/me,
 * which also provisions the user just in time).
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly msal = inject(MsalService);
  private readonly http = inject(HttpClient);

  private readonly accountState = signal<AccountInfo | null>(null);
  private readonly meState = signal<MeResponse | null>(null);
  private readonly statusState = signal<SessionStatus>('anonymous');
  private readonly errorState = signal<string | null>(null);
  private readonly signInErrorState = signal<string | null>(null);
  private pending: Promise<SessionStatus> | null = null;

  readonly account = this.accountState.asReadonly();
  readonly me = this.meState.asReadonly();
  readonly status = this.statusState.asReadonly();
  readonly errorMessage = this.errorState.asReadonly();
  /** Why the last Entra redirect failed (cancelled, AADSTS error); kept apart from session loading. */
  readonly signInError = this.signInErrorState.asReadonly();
  readonly isAdmin = computed(() => this.meState()?.role === 'Admin');

  /** The signed-in MSAL account (active, else the first cached one), or null. */
  currentAccount(): AccountInfo | null {
    const instance = this.msal.instance;
    return instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null;
  }

  /** Loads the session once; later calls return the same result. */
  bootstrap(): Promise<SessionStatus> {
    this.pending ??= this.load();
    return this.pending;
  }

  /** Reloads /me (e.g. after the organization was renamed), keeping the current values until it answers. */
  refresh(): Promise<SessionStatus> {
    this.pending = null;
    return this.bootstrap();
  }

  /** Where this user should be: their dashboard, or the page that explains why not. */
  homeUrl(): string {
    const me = this.meState();
    switch (this.statusState()) {
      case 'ready':
        return me ? `/${me.tenant.slug}/${me.role === 'Admin' ? 'admin' : 'user'}` : '/login';
      case 'unregistered':
        return '/unregistered';
      case 'forbidden':
        return '/forbidden';
      default:
        return '/login';
    }
  }

  async login(): Promise<void> {
    this.signInErrorState.set(null);
    try {
      await firstValueFrom(this.msal.loginRedirect({ scopes: [environment.msal.apiScope] }));
    } catch (error) {
      // A second click while the first redirect is starting: the first one wins, nothing to do.
      if (!(error instanceof BrowserAuthError && error.errorCode === BrowserAuthErrorCodes.interactionInProgress)) {
        throw error;
      }
    }
  }

  async logout(): Promise<void> {
    const account = this.currentAccount();
    this.clear();
    await firstValueFrom(
      this.msal.logoutRedirect({ account, postLogoutRedirectUri: `${window.location.origin}/login` }),
    );
  }

  /** The API rejected our token (401): stop and ask the user to sign in again. No automatic retry (no loops). */
  markUnauthorized(): void {
    this.statusState.set('error');
    this.errorState.set('Your session has expired or was rejected. Please sign in again.');
  }

  /** The sign-in redirect came back with an error; shown on the Login page. */
  reportSignInError(message: string): void {
    this.signInErrorState.set(message);
  }

  /** Remembers why the API said 403, for the /forbidden page. */
  reportForbidden(detail: string | null): void {
    this.errorState.set(detail ?? 'You do not have access to this page.');
  }

  private clear(): void {
    this.pending = null;
    this.accountState.set(null);
    this.meState.set(null);
    this.statusState.set('anonymous');
    this.errorState.set(null);
  }

  private async load(): Promise<SessionStatus> {
    const account = this.currentAccount();
    this.accountState.set(account);

    if (!account) {
      this.pending = null; // don't cache "anonymous": a sign-in may follow
      return this.finish('anonymous');
    }

    this.statusState.set('loading');
    try {
      const tenant = await firstValueFrom(this.http.get<MyTenantResponse>(`${environment.apiBaseUrl}/api/platform/my-tenant`));
      const me = await firstValueFrom(
        this.http.get<MeResponse>(`${environment.apiBaseUrl}/api/${encodeURIComponent(tenant.slug)}/me`),
      );
      this.meState.set(me);
      return this.finish('ready');
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return this.finish('unregistered');
      }
      if (error instanceof HttpErrorResponse && error.status === 403) {
        return this.finish('forbidden', problemDetail(error) ?? 'You do not have access to this organization.');
      }
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.markUnauthorized();
        return 'error';
      }
      this.pending = null; // transient (API down, network): allow a retry
      return this.finish('error', 'We could not load your account. Please try again.');
    }
  }

  private finish(status: SessionStatus, error: string | null = null): SessionStatus {
    this.statusState.set(status);
    this.errorState.set(error);
    return status;
  }
}
