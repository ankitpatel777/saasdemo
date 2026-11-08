import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  GuardResult,
  MaybeAsync,
  Router,
  RouterStateSnapshot,
  UrlSegment,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { firstValueFrom, isObservable } from 'rxjs';
import { SessionService } from '../services/session.service';
import { mockSession, testMe } from '../testing/session-mocks';
import { adminGuard } from './admin.guard';
import { authGuard } from './auth.guard';
import { tenantGuard } from './tenant.guard';

/** A snapshot for /{tenant}/{page}, as the router would build for ':tenant/admin' or ':tenant/user'. */
function routeFor(tenant: string, page: string): ActivatedRouteSnapshot {
  return {
    paramMap: convertToParamMap({ tenant }),
    url: [new UrlSegment(tenant, {}), new UrlSegment(page, {})],
  } as unknown as ActivatedRouteSnapshot;
}

async function run(guard: typeof authGuard, session: SessionService, route = routeFor('demo', 'admin')): Promise<string | true> {
  TestBed.resetTestingModule(); // allows several runs per test
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: SessionService, useValue: session }],
  });

  const outcome: MaybeAsync<GuardResult> = TestBed.runInInjectionContext(() =>
    guard(route, {} as RouterStateSnapshot),
  );
  const result = isObservable(outcome) ? await firstValueFrom(outcome) : await outcome;

  if (result === true) {
    return true;
  }
  expect(result).toBeInstanceOf(UrlTree);
  return TestBed.inject(Router).serializeUrl(result as UrlTree);
}

describe('authGuard', () => {
  it('allows a signed-in user', async () => {
    expect(await run(authGuard, mockSession('anonymous'))).toBe(true);
  });

  it('sends a signed-out user to /login', async () => {
    expect(await run(authGuard, mockSession('anonymous', null, null))).toBe('/login');
  });
});

describe('tenantGuard', () => {
  it('allows the user on their own tenant', async () => {
    expect(await run(tenantGuard, mockSession('ready', testMe('Admin')), routeFor('demo', 'admin'))).toBe(true);
  });

  it("replaces another tenant's slug with the user's own, on the same page", async () => {
    expect(await run(tenantGuard, mockSession('ready', testMe('Admin')), routeFor('other', 'admin'))).toBe('/demo/admin');
    expect(await run(tenantGuard, mockSession('ready', testMe('User')), routeFor('other', 'user'))).toBe('/demo/user');
  });

  it.each([
    ['unregistered', '/unregistered'],
    ['forbidden', '/forbidden'],
    ['error', '/login'],
    ['anonymous', '/login'],
  ] as const)('sends a %s session to %s', async (status, url) => {
    expect(await run(tenantGuard, mockSession(status))).toBe(url);
  });
});

describe('adminGuard', () => {
  it('allows an Admin', async () => {
    expect(await run(adminGuard, mockSession('ready', testMe('Admin')))).toBe(true);
  });

  it("sends a User to their own user dashboard", async () => {
    expect(await run(adminGuard, mockSession('ready', testMe('User')))).toBe('/demo/user');
  });

  it('sends a session that is not ready to its explanation page', async () => {
    expect(await run(adminGuard, mockSession('forbidden'))).toBe('/forbidden');
  });
});
