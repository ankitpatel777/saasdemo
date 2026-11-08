import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { EnvironmentProviders, Provider, inject, provideAppInitializer } from '@angular/core';
import {
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalInterceptor,
  MsalInterceptorConfiguration,
  MsalService,
} from '@azure/msal-angular';
import {
  AuthenticationResult,
  BrowserCacheLocation,
  IPublicClientApplication,
  InteractionType,
  PublicClientApplication,
} from '@azure/msal-browser';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SessionService } from '../services/session.service';

const api = environment.apiBaseUrl;

/** Authorization Code + PKCE against any Entra organization; tokens kept per browser tab. */
export function createMsalInstance(): IPublicClientApplication {
  return new PublicClientApplication({
    auth: {
      clientId: environment.msal.clientId,
      authority: environment.msal.authority,
      redirectUri: window.location.origin,
      postLogoutRedirectUri: `${window.location.origin}/login`,
    },
    cache: {
      // sessionStorage: tokens disappear when the tab closes and aren't shared across tabs.
      cacheLocation: BrowserCacheLocation.SessionStorage,
    },
  });
}

/**
 * Which requests get a bearer token. Patterns match the WHOLE URL path ('*' is a wildcard) and the
 * FIRST matching entry wins, so the anonymous health entries must come before the catch-all.
 * Health is listed as two exact patterns: '/api/health*' would also swallow a tenant slug such as
 * 'healthcare'.
 */
export function createMsalInterceptorConfig(): MsalInterceptorConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap: new Map<string, string[] | null>([
      [`${api}/api/health`, null],
      [`${api}/api/health/*`, null],
      [`${api}/api/*`, [environment.msal.apiScope]],
    ]),
  };
}

/**
 * Completes a sign-in redirect before the router starts, so guards already see the account:
 * initialize MSAL, process the #code response (if any), then pick the active account.
 */
export async function initializeMsal(msal: MsalService, session: SessionService): Promise<void> {
  await firstValueFrom(msal.initialize());

  let result: AuthenticationResult | null = null;
  try {
    result = await firstValueFrom(msal.handleRedirectObservable());
  } catch (error) {
    // Cancelled consent or an AADSTS error must not stop Angular from booting (blank page).
    // Log only the code and message, never the error object (it may carry request details).
    const code = signInErrorCode(error);
    console.error('Sign-in redirect failed:', code ?? errorCodeOf(error));
    session.reportSignInError(`Sign-in was cancelled or failed. Please try again.${code ? ` (${code})` : ''}`);
  }

  const instance = msal.instance;
  const account = result?.account ?? instance.getActiveAccount() ?? instance.getAllAccounts()[0] ?? null;
  if (account) {
    instance.setActiveAccount(account);
  }
}

function errorCodeOf(error: unknown): string {
  const code = (error as { errorCode?: unknown } | null)?.errorCode;
  return typeof code === 'string' ? code : 'unknown_error';
}

/** The AADSTS code (e.g. AADSTS65004) carried by an MSAL error, if any. */
export function signInErrorCode(error: unknown): string | null {
  // Read fields structurally: instanceof is unreliable when MSAL is bundled more than once.
  const e = (typeof error === 'object' && error !== null ? error : {}) as Partial<
    Record<'errorNo' | 'errorCode' | 'subError' | 'errorMessage' | 'message', unknown>
  >;
  if (typeof e.errorNo === 'string' && /^\d+$/.test(e.errorNo)) {
    return `AADSTS${e.errorNo}`; // Entra's numeric error code
  }
  const text = [e.errorCode, e.subError, e.errorMessage, e.message, typeof error === 'string' ? error : '']
    .filter((part) => typeof part === 'string')
    .join(' ');
  return /AADSTS\d+/.exec(text)?.[0] ?? null;
}

/** MSAL for standalone apps: no MsalModule. Requires provideHttpClient(withInterceptorsFromDi()). */
export function provideMsal(): (Provider | EnvironmentProviders)[] {
  return [
    { provide: MSAL_INSTANCE, useFactory: createMsalInstance },
    { provide: MSAL_INTERCEPTOR_CONFIG, useFactory: createMsalInterceptorConfig },
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true },
    MsalService,
    MsalBroadcastService,
    provideAppInitializer(() => initializeMsal(inject(MsalService), inject(SessionService))),
  ];
}
