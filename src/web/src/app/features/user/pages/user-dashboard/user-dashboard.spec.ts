import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../../environments/environment';
import { SessionService } from '../../../../core/services/session.service';
import { TenantService } from '../../../../core/services/tenant.service';
import { mockSession, mockTenantService, testMe } from '../../../../core/testing/session-mocks';
import { UserDashboard } from './user-dashboard';

const activityUrl = `${environment.apiBaseUrl}/api/demo/me/activity`;
const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('UserDashboard', () => {
  let fixture: ComponentFixture<UserDashboard>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserDashboard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: mockSession('ready', testMe('User')) },
        { provide: TenantService, useValue: mockTenantService() },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UserDashboard);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;

  async function settle(): Promise<void> {
    await tick();
    fixture.detectChanges();
  }

  it('shows my profile from the session', () => {
    http.expectOne(activityUrl).flush([]);

    const card = el().querySelector('[data-testid="profile-card"]')!.textContent;
    expect(card).toContain('Me Myself');
    expect(card).toContain('me@example.com');
    expect(card).toContain('User');
    expect(card).toContain('Demo Client');
  });

  it('shows a skeleton, then my recent sign-ins', async () => {
    expect(el().querySelector('[data-testid="skeleton"]')).not.toBeNull();

    http.expectOne(activityUrl).flush([{ occurredAtUtc: '2026-11-08T09:00:00Z' }, { occurredAtUtc: '2026-11-07T09:00:00Z' }]);
    await settle();

    expect(el().querySelectorAll('[data-testid="sign-ins"] mat-list-item').length).toBe(2);
    expect(el().querySelector('[data-testid="sign-ins"] time')?.getAttribute('datetime')).toBe('2026-11-08T09:00:00Z');
  });

  it('shows an empty state when there are no sign-ins', async () => {
    http.expectOne(activityUrl).flush([]);
    await settle();

    expect(el().querySelector('[data-testid="empty-state"]')?.textContent).toContain('No sign-ins');
  });

  it('shows an error state with a retry', async () => {
    http.expectOne(activityUrl).flush({}, { status: 500, statusText: 'Server Error' });
    await settle();

    const error = el().querySelector('[data-testid="error-state"]')!;
    expect(error.textContent).toContain("couldn't load");

    (error.querySelector('button') as HTMLButtonElement).click();
    await settle();
    http.expectOne(activityUrl).flush([]);
  });
});
