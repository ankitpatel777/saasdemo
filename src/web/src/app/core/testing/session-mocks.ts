import { signal } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { AccountInfo } from '@azure/msal-browser';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { MeResponse, SessionService, SessionStatus } from '../services/session.service';
import { TenantService } from '../services/tenant.service';

export const testAccount = { username: 'me@example.com', homeAccountId: 'h', localAccountId: 'l' } as AccountInfo;

export function testMe(role: 'Admin' | 'User' = 'Admin', slug = 'demo'): MeResponse {
  return { id: '1', email: 'me@example.com', displayName: 'Me Myself', role, tenant: { slug, name: 'Demo Client' } };
}

/** A MsalService stand-in: just the account lookups and redirects the app uses. */
export function mockMsalService(account: AccountInfo | null = testAccount) {
  return {
    instance: {
      getActiveAccount: vi.fn(() => account),
      getAllAccounts: vi.fn(() => (account ? [account] : [])),
    },
    loginRedirect: vi.fn(() => of(undefined)),
    logoutRedirect: vi.fn(() => of(undefined)),
  } as unknown as MsalService & {
    loginRedirect: ReturnType<typeof vi.fn>;
    logoutRedirect: ReturnType<typeof vi.fn>;
  };
}

/** A SessionService stand-in with a fixed outcome, for guards and components. */
export function mockSession(status: SessionStatus, me: MeResponse | null = null, account: AccountInfo | null = testAccount) {
  const homeUrl =
    status === 'ready' && me
      ? `/${me.tenant.slug}/${me.role === 'Admin' ? 'admin' : 'user'}`
      : status === 'unregistered'
        ? '/unregistered'
        : status === 'forbidden'
          ? '/forbidden'
          : '/login';

  return {
    me: signal(me).asReadonly(),
    status: signal(status).asReadonly(),
    account: signal(account).asReadonly(),
    errorMessage: signal<string | null>(null).asReadonly(),
    signInError: signal<string | null>(null).asReadonly(),
    isAdmin: signal(me?.role === 'Admin').asReadonly(),
    currentAccount: vi.fn(() => account),
    bootstrap: vi.fn(() => Promise.resolve(status)),
    homeUrl: vi.fn(() => homeUrl),
    login: vi.fn(() => Promise.resolve()),
    logout: vi.fn(() => Promise.resolve()),
    refresh: vi.fn(() => Promise.resolve(status)),
    markUnauthorized: vi.fn(),
    reportForbidden: vi.fn(),
    reportSignInError: vi.fn(),
  } as unknown as SessionService;
}

/** TenantService stand-in: API URLs for tenant "demo", without needing a routed page. */
export function mockTenantService(slug = 'demo') {
  return {
    currentTenant: signal<string | null>(slug).asReadonly(),
    apiUrl: (path: string) => `${environment.apiBaseUrl}/api/${slug}/${path.replace(/^\/+/, '')}`,
  } as unknown as TenantService;
}
