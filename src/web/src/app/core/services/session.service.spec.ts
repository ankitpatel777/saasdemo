import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MsalService } from '@azure/msal-angular';
import { AccountInfo } from '@azure/msal-browser';
import { environment } from '../../../environments/environment';
import { mockMsalService, testAccount, testMe } from '../testing/session-mocks';
import { SessionService } from './session.service';

const api = environment.apiBaseUrl;
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('SessionService.bootstrap', () => {
  let session: SessionService;
  let http: HttpTestingController;
  let msal: ReturnType<typeof mockMsalService>;

  function setup(account: AccountInfo | null = testAccount): void {
    msal = mockMsalService(account);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: MsalService, useValue: msal }],
    });
    session = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  }

  /** Answers /my-tenant, then (if given) /{slug}/me. */
  async function respond(myTenant: object | [number, object], me?: object | [number, object]): Promise<void> {
    const reply = (url: string, body: object | [number, object]) => {
      const req = http.expectOne(url);
      if (Array.isArray(body)) {
        req.flush(body[1], { status: body[0], statusText: 'error' });
      } else {
        req.flush(body);
      }
    };

    reply(`${api}/api/platform/my-tenant`, myTenant);
    if (me) {
      await tick(); // the /me request starts after /my-tenant resolves
      reply(`${api}/api/demo/me`, me);
    }
  }

  afterEach(() => http.verify());

  it('is anonymous without an account, makes no API calls, and points to /login', async () => {
    setup(null);

    expect(await session.bootstrap()).toBe('anonymous');
    expect(session.homeUrl()).toBe('/login');
  });

  it('routes an Admin to /{slug}/admin', async () => {
    setup();
    const result = session.bootstrap();
    expect(session.status()).toBe('loading');

    await respond({ slug: 'demo', name: 'Demo Client' }, testMe('Admin'));

    expect(await result).toBe('ready');
    expect(session.isAdmin()).toBe(true);
    expect(session.me()?.tenant.name).toBe('Demo Client');
    expect(session.homeUrl()).toBe('/demo/admin');
  });

  it('routes a User to /{slug}/user', async () => {
    setup();
    const result = session.bootstrap();
    await respond({ slug: 'demo', name: 'Demo Client' }, testMe('User'));

    expect(await result).toBe('ready');
    expect(session.isAdmin()).toBe(false);
    expect(session.homeUrl()).toBe('/demo/user');
  });

  it('routes an unregistered organization (my-tenant 404) to /unregistered', async () => {
    setup();
    const result = session.bootstrap();
    await respond([404, { title: 'Organization not registered' }]);

    expect(await result).toBe('unregistered');
    expect(session.homeUrl()).toBe('/unregistered');
  });

  it('routes a 403 from /me to /forbidden and keeps the API detail', async () => {
    setup();
    const result = session.bootstrap();
    await respond({ slug: 'demo', name: 'Demo Client' }, [403, { detail: 'No role assigned in your organization.' }]);

    expect(await result).toBe('forbidden');
    expect(session.errorMessage()).toBe('No role assigned in your organization.');
    expect(session.homeUrl()).toBe('/forbidden');
  });

  it('reports a transient failure as error and allows a retry', async () => {
    setup();
    const first = session.bootstrap();
    await respond([500, {}]);
    expect(await first).toBe('error');
    expect(session.homeUrl()).toBe('/login');

    const retry = session.bootstrap();
    await respond({ slug: 'demo', name: 'Demo Client' }, testMe('Admin'));
    expect(await retry).toBe('ready');
  });

  it('caches the result for the app lifetime', async () => {
    setup();
    const first = session.bootstrap();
    await respond({ slug: 'demo', name: 'Demo Client' }, testMe('Admin'));
    await first;

    expect(await session.bootstrap()).toBe('ready'); // http.verify(): no second round of requests
  });

  it('logout clears the session and redirects through MSAL', async () => {
    setup();
    const first = session.bootstrap();
    await respond({ slug: 'demo', name: 'Demo Client' }, testMe('Admin'));
    await first;

    await session.logout();

    expect(session.me()).toBeNull();
    expect(session.status()).toBe('anonymous');
    expect(msal.logoutRedirect).toHaveBeenCalledOnce();
  });
});
