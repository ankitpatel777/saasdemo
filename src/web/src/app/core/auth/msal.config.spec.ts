import { MsalService } from '@azure/msal-angular';
import { AuthenticationResult, ServerError } from '@azure/msal-browser';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { SessionService } from '../services/session.service';
import { testAccount } from '../testing/session-mocks';
import { initializeMsal, signInErrorCode } from './msal.config';

function setup(redirect: ReturnType<MsalService['handleRedirectObservable']>) {
  const instance = {
    getActiveAccount: vi.fn(() => null),
    getAllAccounts: vi.fn(() => []),
    setActiveAccount: vi.fn(),
  };
  const msal = {
    instance,
    initialize: vi.fn(() => of(undefined)),
    handleRedirectObservable: vi.fn(() => redirect),
  } as unknown as MsalService;
  const session = { reportSignInError: vi.fn() } as unknown as SessionService & {
    reportSignInError: ReturnType<typeof vi.fn>;
  };
  return { msal, session, instance };
}

describe('initializeMsal', () => {
  afterEach(() => vi.restoreAllMocks());

  it('sets the account returned by a successful redirect', async () => {
    const { msal, session, instance } = setup(of({ account: testAccount } as AuthenticationResult));

    await initializeMsal(msal, session);

    expect(instance.setActiveAccount).toHaveBeenCalledWith(testAccount);
    expect(session.reportSignInError).not.toHaveBeenCalled();
  });

  it('keeps booting when Entra returns an error, and reports it with the AADSTS code', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const error = new ServerError('access_denied', 'corr-id', 'AADSTS65004: User declined to consent.', '', '65004');
    const { msal, session, instance } = setup(throwError(() => error));

    await expect(initializeMsal(msal, session)).resolves.toBeUndefined();

    expect(session.reportSignInError).toHaveBeenCalledWith(
      'Sign-in was cancelled or failed. Please try again. (AADSTS65004)',
    );
    expect(consoleError).toHaveBeenCalledWith('Sign-in redirect failed:', 'AADSTS65004');
    expect(instance.setActiveAccount).not.toHaveBeenCalled();
  });

  it('reports an error without an AADSTS code using the generic message', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { msal, session } = setup(throwError(() => new Error('network down')));

    await initializeMsal(msal, session);

    expect(session.reportSignInError).toHaveBeenCalledWith('Sign-in was cancelled or failed. Please try again.');
  });
});

describe('signInErrorCode', () => {
  it('finds the code in the message, or returns null', () => {
    expect(signInErrorCode(new ServerError('invalid_request', 'corr-id', 'AADSTS50011: redirect mismatch'))).toBe('AADSTS50011');
    expect(signInErrorCode(new ServerError('invalid_request', 'corr-id', 'x', '', '50011'))).toBe('AADSTS50011');
    expect(signInErrorCode('nothing here')).toBeNull();
  });
});
