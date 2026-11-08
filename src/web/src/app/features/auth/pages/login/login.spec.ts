import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { HealthResponse, HealthService } from '../../../../core/services/health';
import { SessionService } from '../../../../core/services/session.service';
import { mockSession, testMe } from '../../../../core/testing/session-mocks';
import { Login } from './login';

describe('Login', () => {
  async function render(
    session: SessionService,
    health$: Observable<HealthResponse> = of({ status: 'ok', version: '1', timestampUtc: '' }),
  ): Promise<ComponentFixture<Login>> {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        { provide: HealthService, useValue: { getHealth: () => health$ } },
        { provide: SessionService, useValue: session },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Login);
    await fixture.whenStable();
    return fixture;
  }

  const text = (f: ComponentFixture<Login>, id: string) =>
    (f.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`)?.textContent;

  it('shows the API status', async () => {
    const f = await render(mockSession('anonymous', null, null));
    expect(text(f, 'api-status')).toContain('API status: ok');
  });

  it('shows why the last sign-in redirect failed', async () => {
    const session = mockSession('anonymous', null, null);
    (session as unknown as { signInError: () => string | null }).signInError = () =>
      'Sign-in was cancelled or failed. Please try again. (AADSTS65004)';
    const f = await render(session);
    expect(text(f, 'sign-in-error')).toContain('(AADSTS65004)');
  });

  it('shows unreachable when the API call fails', async () => {
    const f = await render(mockSession('anonymous', null, null), throwError(() => new Error('down')));
    expect(text(f, 'api-status')).toContain('API status: unreachable');
  });

  it('starts the Microsoft sign-in redirect from the button', async () => {
    const session = mockSession('anonymous', null, null);
    const f = await render(session);

    (f.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="sign-in"]')!.click();
    await f.whenStable();

    expect(session.login).toHaveBeenCalledOnce();
    expect(text(f, 'sign-in')).toContain('Redirecting to Microsoft');
  });

  it('sends a signed-in Admin to their admin dashboard', async () => {
    const navigate = vi.spyOn(Router.prototype, 'navigateByUrl').mockResolvedValue(true);
    await render(mockSession('ready', testMe('Admin')));

    expect(navigate).toHaveBeenCalledWith('/demo/admin');
    navigate.mockRestore();
  });
});
